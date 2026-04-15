using System.ComponentModel.DataAnnotations;

namespace BIM.Application.Common.Configs
{
    public class LabelStarSettings : IValidatableObject
    {
        public const string SectionName = nameof(LabelStarSettings);
        public string FileName { get; set; } = string.Empty;
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrEmpty(FileName))
                yield return new ValidationResult($"{nameof(LabelStarSettings)}.{nameof(FileName)} is not configured!",
                    new[] { nameof(FileName) });
        }
    }
}
