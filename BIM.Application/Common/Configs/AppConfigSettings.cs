using System.ComponentModel.DataAnnotations;

namespace BIM.Application.Common.Configs
{
    public class AppConfigSettings : IValidatableObject
    {
        public const string SectionName = nameof(AppConfigSettings);
        public string PC_Name { get; set; } = string.Empty;
        public string UserDbFile { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrEmpty(PC_Name))
                yield return new ValidationResult($"{nameof(AppConfigSettings)}.{nameof(PC_Name)} is not configured!",
                    new[] { nameof(PC_Name) });

            if (string.IsNullOrEmpty(UserDbFile))
                yield return new ValidationResult($"{nameof(AppConfigSettings)}.{nameof(UserDbFile)} is not configured!",
                    new[] { nameof(UserDbFile) });
        }
    }
}
