using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MimeKit.Text;
using MimeKit;
using SampleProject.Data;
using SampleProject.Models.DTO;
using SampleProject.Models.ProductInventory;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Caching.Memory;

namespace SampleProject.Controllers
{
    [Route("api/")]
    [ApiController]
    public class ProductInventoryController : ControllerBase
    {
        private readonly ProductInventoryContext _db;
        private IConfiguration _config;
        private const string otp = "OtpKey";
        private readonly IMemoryCache _cache;


        public ProductInventoryController(IConfiguration configuration, ProductInventoryContext db, IMemoryCache cache) { 
            _db = db;
            _config = configuration;
            _cache = cache;
        }
        public class Validate
        {
            public string otp { get; set; }
        }

        public class SendEmailDto
        {
            public string to { get; set; }
        }

        private User Authentication(Login login)
        {
            User _user = null;
            if (login.UserName != null && login.Password != null)
            {
                var u = _db.Logins.FirstOrDefault(x => x.UserName == login.UserName);
                if (u != null) {
                    var isValidPassword = BCrypt.Net.BCrypt.Verify(login.Password, u.Password);
                    if (isValidPassword)
                    {
                        _user = _db.Users.FirstOrDefault(us => us.Name == login.UserName);
                    }
                }
            }

            return _user;
        }

        private string GenerateToken()
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);

            var token = new JwtSecurityToken(
                _config["Jwt:Issuer"], _config["Jwt:Audience"], null,
                expires: DateTime.UtcNow.AddMinutes(60),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);

        }

        [HttpPost("user/sendMail")]
        public IActionResult SendEmail([FromBody] SendEmailDto sender)
        {
            Random generator = new Random();
            string code = generator.Next(0, 1000000).ToString("D6");

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("Sai Vasanth", "20bd1a0556csec@gmail.com"));
            email.To.Add(MailboxAddress.Parse(sender.to));

            email.Subject = "OTP From XYZ";
            email.Body = new TextPart(TextFormat.Html) { Text = $"<p>Your otp for registration <strong>{code}</strong></p>" };

            using var smtp = new SmtpClient();
            smtp.Connect("smtp.gmail.com", 465, useSsl: true);
            smtp.Authenticate("20bd1a0556csec@gmail.com", _config.GetSection("Password").Value);
            smtp.Send(email);
            smtp.Disconnect(true);

            _cache.Set(otp, code.ToString(), TimeSpan.FromMinutes(5)); // Set OTP in cache for 5 minutes
            return Ok(new {result="Sent Mail!"});
        }


        [HttpPost("user/verify")]
        public IActionResult ValidateOtp([FromBody] Validate values)
        {

            if (_cache.TryGetValue(otp, out string cachedOtp) && cachedOtp == values.otp)
            {
                return Ok(new {result="OTP is valid" });
            }
            return Ok(new { result="Invalid OTP" });
        }
        

        [HttpPost("user/signup")]
        public async Task<IActionResult> Signup([FromBody]SignupDto user)
        {
            if(user == null) { return Ok("In-sufficient details!");  }

            var profileImage = "https://doonofficersacademy.com/wp-content/uploads/2022/10/sample-profile.png";

            var User = _db.Users.FirstOrDefault(u => u.Email == user.Email);
            if (User != null) { return Ok("User exists"); }


            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(user.Password);

            // Adding user detials into database, in users table.
            await _db.Database.ExecuteSqlRawAsync(
                "EXEC spAddUser @UserName, @Gender, @Email, @ProfileImage",
                new SqlParameter("@UserName", user.Name),
                new SqlParameter("@Gender", user.Gender),
                new SqlParameter("@Email", user.Email),
                new SqlParameter("@ProfileImage", profileImage)
            );


            var fetchId = await _db.ProductIds.FromSqlRaw("SELECT IDENT_CURRENT('Users') AS ID").ToListAsync();
            var ID = fetchId[0].ID;

            // Adding login details of a user in database, in login table.
            await _db.Database.ExecuteSqlRawAsync(
                "EXEC spAddLogin @Id, @UserName, @Password",
                new SqlParameter("@Id", ID),
                new SqlParameter("@UserName", user.Name),
                new SqlParameter("@Password", hashedPassword)
            );

            return Ok(new { result = "User Added :)" });
        }

        [AllowAnonymous]
        [HttpPost("user/login")]
        public IActionResult UserLogin([FromBody] Login login)
        {
            IActionResult res = Unauthorized();
            var user_ = Authentication(login);
            if (user_ != null)
            {
                var token = GenerateToken();
                res = Ok(new {user = user_, token = token });
            }

            return res;
        }

        [HttpGet("user/me")]
        public IActionResult CheckTokenExpiry()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            

            if (string.IsNullOrEmpty(authHeader))
            {
                return Unauthorized(new { message = "Token is missing or invalid" });
            }

            var token = authHeader.Trim();

            var signingKey = _config["Jwt:Key"];
            var tokenHandler = new JwtSecurityTokenHandler();

            try
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidIssuer = _config["Jwt:Issuer"],
                    ValidAudience = _config["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                if (validatedToken is JwtSecurityToken jwtSecurityToken)
                {
                    if (jwtSecurityToken.ValidTo < DateTime.UtcNow)
                    {
                        return Unauthorized(new { message = "Token is expired" });
                    }

                    return Ok(new { message = "Token is valid" });
                }
            }
            catch (Exception ex)
            {
                return Unauthorized(new { message = "Token validation failed", error = ex.Message });
            }

            return Unauthorized(new { message = "Token is invalid" });
        }


        [HttpGet("products")]
        public async Task<List<ProductInfo>> GetProducts()
        {
            
            var products = await _db.ProductDtos.FromSqlRaw("spGetProducts").ToListAsync();
            List<ProductInfo> pros = new List<ProductInfo>();

            for(int i =  0; i < products.Count; i++)
            {
                int id = products[i].Id;
                string[] images = _db.Images.Where(i => i.Id == id).Select(i => i.ImageUrl).ToArray();
                var pro = new ProductInfo()
                {
                    id = id,
                    title = products[i].Title,
                    description = products[i].Description,
                    price = products[i].Price,
                    rating = products[i].Rating,
                    brand = products[i].Brand_Name,
                    category = products[i].Category_Name,
                    thumbnail = products[i].Thumbnail,
                    images = images
                };
                pros.Add(pro);
            }


            return pros;    
        }

        [HttpPost("products/addProduct")]
        public async Task<IActionResult> CreateProduct([FromBody] ProductInfo product)
        {
            if (product == null)
            {
                return BadRequest("Product not found!");
            }

            var brand = _db.Brands.FirstOrDefault(b => b.BrandName.ToLower() == product.brand.ToLower());
            var category = _db.Categories.FirstOrDefault(c => c.CategoryName.ToLower() == product.category.ToLower());
            string brandId = brand == null ? null : brand.BrandId;
            string categoryId = category == null ? null : category.CategoryId;

            if (brand == null)
            {
                brandId = product.brand.Substring(0, 2);
                await _db.Database.ExecuteSqlRawAsync(
                    "EXEC addBrand @BrandId, @BrandName",
                    new SqlParameter("@BrandId", brandId),
                    new SqlParameter("@BrandName", product.brand)
                );
            }
            if (category == null)
            {
                categoryId = product.category.Substring(0, 2);
                await _db.Database.ExecuteSqlRawAsync(
                    "EXEC addCategory @CategoryId, @CategoryName",
                    new SqlParameter("@CategoryId", categoryId),
                    new SqlParameter("@CategoryName", product.category)
                );
            }

            await _db.Database.ExecuteSqlRawAsync(
                "EXEC addProduct @Title, @Description, @Price, @Rating, @BrandId, @CategoryId, @Thumbnail",
                new SqlParameter("@Title", product.title),
                new SqlParameter("@Description", product.description),
                new SqlParameter("@Price", product.price),
                new SqlParameter("@Rating", product.rating),
                new SqlParameter("@BrandId", brandId),
                new SqlParameter("@CategoryId", categoryId),
                new SqlParameter("@Thumbnail", product.thumbnail)
            );

            var fetchId = await _db.ProductIds.FromSqlRaw("SELECT IDENT_CURRENT('Products') AS ID").ToListAsync();
            var ID = fetchId[0].ID;

            foreach (var image in product.images)
            {
                await _db.Database.ExecuteSqlRawAsync(
                    "EXEC addImage @ProductId, @Image",
                    new SqlParameter("@ProductId", ID),
                    new SqlParameter("@Image", image)
                );
            }

            return Ok(product);
        }

        [HttpGet("products/{id:int}")]
        public async Task<ActionResult<ProductDto>> GetProductDetails(int id)
        {
            var products = await _db.ProductDtos.FromSqlRaw($"spGetProductDetails {id}").ToListAsync();
            string[] images = _db.Images.Where(i => i.Id == id).Select(i => i.ImageUrl).ToArray();
            var pro = new ProductInfo()
            {
                id = id,
                title = products[0].Title,
                description = products[0].Description,
                price = products[0].Price,
                rating = products[0].Rating,
                brand = products[0].Brand_Name,
                category = products[0].Category_Name,
                thumbnail = products[0].Thumbnail,
                images = images
            };
            return Ok(pro);
        }

        [HttpDelete("products/deleteProduct/{id:int}")]
        public async Task<ActionResult> DeleteProduct(int id)
        {

            var products = await _db.Database.ExecuteSqlRawAsync($"spDeleteProduct {id}");
            
            return Ok(new {result = "Deleted!"});
        }

        [HttpPut("products/update/{id:int}")]
        public async Task<IActionResult> UpdateProduct([FromBody] ProductInfo product, int id)
        {
            if (product == null || (product != null && product.id != id))
            {
                return BadRequest("Product not found!");
            }

            var brand = _db.Brands.FirstOrDefault(b => b.BrandName == product.brand);
            var category = _db.Categories.FirstOrDefault(c => c.CategoryName == product.category);
            string brandId = brand == null ? null : brand.BrandId;
            string categoryId = category == null ? null : category.CategoryId;

            if (brand == null)
            {
                brandId = product.brand.Substring(0, 2) + product.brand.Substring(product.brand.Length - 1);
                await _db.Database.ExecuteSqlRawAsync(
                    "EXEC addBrand @BrandId, @BrandName",
                    new SqlParameter("@BrandId", brandId),
                    new SqlParameter("@BrandName", product.brand)
                );
            }
            if (category == null)
            {
                categoryId = product.category.Substring(0, 2) + product.category.Substring(product.category.Length - 1);
                await _db.Database.ExecuteSqlRawAsync(
                    "EXEC addCategory @CategoryId, @CategoryName",
                    new SqlParameter("@CategoryId", categoryId),
                    new SqlParameter("@CategoryName", product.category)
                );
            }

            await _db.Database.ExecuteSqlRawAsync(
                "EXEC spUpdateProduct @Id, @title, @description, @price, @rating, @brandID, @categoryId",
                new SqlParameter("@Id", product.id),
                new SqlParameter("@title", product.title),
                new SqlParameter("@description", product.description),
                new SqlParameter("@price", product.price),
                new SqlParameter("@rating", product.rating),
                new SqlParameter("@brandId", brandId),
                new SqlParameter("@categoryId", categoryId)
            );

            if(product.thumbnail != "null")
            {
                await _db.Database.ExecuteSqlRawAsync("UPDATE Products SET Thumbnail = @thumb WHERE ID = @id", 
                    new SqlParameter("@thumb", product.thumbnail),
                    new SqlParameter("@id", product.id)
                );
            }

            if(product.images[0] != null)
            {
                await _db.Database.ExecuteSqlRawAsync("DELETE FROM Images WHERE ID = @Id;", new SqlParameter("@Id", id));
            
                foreach (var image in product.images)
                {
                    await _db.Database.ExecuteSqlRawAsync(
                        "EXEC addImage @ProductId, @Image",
                        new SqlParameter("@ProductId", id),
                        new SqlParameter("@Image", image)
                    );
                }
            }
            return Ok(product);
        }

        [HttpGet("products/search/{value}")]
        public async Task<ActionResult<ProductInfo>> SearchProducts(string value)
        {
            List<ProductInfo> products = new List<ProductInfo>();
            products = await GetProducts();

            List<ProductInfo> searchResult = new List<ProductInfo>();
            products.ForEach(product => {
                if(product.title.ToLower().IndexOf(value.ToLower()) != -1 || product.description.ToLower().IndexOf(value.ToLower()) != -1)
                {
                    searchResult.Add(product);
                }
            });
            return Ok(searchResult);
        }

        [HttpGet("products/categories")]
        public ActionResult Categories ()
        {
            var categories = _db.categoryDtos.FromSqlRaw("spGetCategories");
            List<string> categoriesList = new List<string>();
            foreach (var category in categories) {
                categoriesList.Add(category.Category_Name);
            }
            return Ok(categoriesList);
        }
    }
}
