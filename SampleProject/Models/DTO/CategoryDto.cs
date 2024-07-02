using System.ComponentModel.DataAnnotations;

namespace SampleProject.Models.DTO
{
    public class CategoryDto
    {
        [Key]
        public string Category_Name { get; set; }
    }
}
