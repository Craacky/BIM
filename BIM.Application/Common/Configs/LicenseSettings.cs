using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace BIM.Application.Common.Configs
{
    public class LicenseSettings : IValidatableObject
    {
        public const string SectionName = "LicenseSettings";
        public string SecretKey { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrEmpty(SecretKey))
                yield return new ValidationResult($"{nameof(LicenseSettings)}.{nameof(SecretKey)} is not configured!",
                    new[] { nameof(SecretKey) });
        }
    }
}
