using System.ComponentModel.DataAnnotations;

namespace BIM.Application.Common.Configs
{
    public class DatabaseSettings : IValidatableObject
    {
        public const string SectionName = nameof(DatabaseSettings);
        public string DbProvider { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrEmpty(DbProvider))
                yield return new ValidationResult($"{nameof(DatabaseSettings)}.{nameof(DbProvider)} is not configured!",
                    new[] { nameof(DbProvider) });
            if (string.IsNullOrEmpty(ConnectionString))
                yield return new ValidationResult($"{nameof(DatabaseSettings)}.{nameof(ConnectionString)} is not configured!",
                    new[] { nameof(ConnectionString) });
        }
    }
}
