using BIM.Application.Common.Mapping;
using BIM.Domain.Entities;

namespace BIM.Application.Features.Products.DTO
{
    public class ProductDTO : IMapFrom<Product>
    {
        public string Id { get; set; }
        public string GTIN { get; set; }
        public string AboutProduct { get; set; }

        public override int GetHashCode() => this.Id.GetHashCode();

        public override bool Equals(object obj)
        {
            if (!(obj is ProductDTO))
                throw new ArgumentException("obj is not an ProductDTO");
            var user = obj as ProductDTO;
            if (user == null)
                return false;
            return this.Id.Equals(user.Id);
        }
    }
}
