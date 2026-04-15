using BIM.Application.Common.Mapping;
using BIM.Domain.Entities;
using BIM.Domain.Enums;

namespace BIM.Application.Features.Databases.DTO
{
    public class DatabaseListDTO : IMapFrom<DatabaseList>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string FirstCode { get; set; }
        public DbStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
