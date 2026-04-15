namespace BIM.Domain.Entities
{
    public class Logger
    {
        public int Id { get; set; }
        public string? Message { get; set; }
        public string? MessageTemplate { get; set; }
        public string Level { get; set; }

        public string? UserName { get; set; }
        public string? ClientIP { get; set; }
        public string? ClientAgent { get; set; }
        public string? Exception { get; set; }
        public string? Propperties { get; set; }
        public string? LogEvent { get; set; }
        public DateTime TimeStamp { get; set; } = DateTime.Now;
    }
}
