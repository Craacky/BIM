using System.ComponentModel.DataAnnotations;

namespace BIM.Domain.Entities
{
    public class Product
    {
        [Key]
        public int Id { get; set; }
        public string GTIN { get; set; }
        public string AboutProduct { get; set; }
        //public Product() => Id = Guid.NewGuid().ToString();
    }
}
