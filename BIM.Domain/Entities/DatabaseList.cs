using BIM.Domain.Enums;

namespace BIM.Domain.Entities
{
    public class DatabaseList
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string FirstCode { get; set; }
        public DateTime CreatedDate { get; set; }
        public DbStatus Status { get; set; }
    }
}
