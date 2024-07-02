using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SampleProject.Data;
using SampleProject.Models.DTO;
using SampleProject.Models.ProductInventory;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace SampleProject.Controllers
{
    [Route("api/")]
    [ApiController]
    public class ProductInventoryController : ControllerBase
    {
        private readonly ProductInventoryContext _db;
        private IConfiguration _config;


        public ProductInventoryController(IConfiguration configuration, ProductInventoryContext db) { 
            _db = db;
            _config = configuration;
        }

        private User Authentication(Login login)
        {
            User _user = null;
            if (login.UserName != null && login.Password != null)
            {
                var u = _db.Logins.FirstOrDefault(x => x.Password == login.Password);
                if (u != null) {
                    if (u.UserName == login.UserName) {
                        _user = _db.Users.FirstOrDefault(us => us.Id == u.Id);
                    }
                }
            }

            return _user;
        }

        private string GenerateToken()
        {
            Console.WriteLine(_config["Jwt:Audience"]);
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);

            var token = new JwtSecurityToken(
                _config["Jwt:Issuer"], _config["Jwt:Audience"], null,
                expires: DateTime.UtcNow.AddMinutes(60),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);

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

        [AllowAnonymous]
        [HttpPost("user/login")]
        public IActionResult UserLogin([FromBody] Login login)
        {
            IActionResult res = Unauthorized();
            var user_ = Authentication(login);
            if (user_ != null)
            {
                var token = GenerateToken();
                res = Ok(new {name = user_.Name, token = token });
            }

            return res;
        }

        [HttpGet("products")]
        public async Task<List<ProductInfo>> GetProducts()
        {
            
            var products = await _db.ProductDtos.FromSqlRaw("spGetProducts").ToListAsync();
            List<ProductInfo> pros = new List<ProductInfo>();

            for(int i =  0; i < products.Count; i++)
            {
                int id = products[i].Id;
                Console.WriteLine(id);
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
            List<String> categoriesList = new List<String>();
            foreach (var category in categories) {
                categoriesList.Add(category.Category_Name);
            }
            return Ok(categoriesList);
        }
    }
}
