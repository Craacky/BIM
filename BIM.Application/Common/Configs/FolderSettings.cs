using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BIM.Application.Common.Configs
{
    public class FolderSettings : IValidatableObject
    {
        public const string SectionName = nameof(FolderSettings);
        public string Path { get; set; } = string.Empty;
        public string Good { get; set; } = string.Empty;
        public string TemporaryStorage { get; set; } = string.Empty;
        public string Archive { get; set; } = string.Empty;
        public string StatisticsOutput { get; set; } = string.Empty; // New property
        public string CameraStatisticsOutput { get; set; } = string.Empty;
        public string Duplicates { get; set; } = string.Empty; // Property for duplicates folder
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrEmpty(Path))
                yield return new ValidationResult($"{nameof(FolderSettings)}.{nameof(Path)} is not configured!",
                    new[] { nameof(Path) });
            if (string.IsNullOrEmpty(Good))
                yield return new ValidationResult($"{nameof(FolderSettings)}.{nameof(Good)} is not configured!",
                    new[] { nameof(Good) });
            if (string.IsNullOrEmpty(TemporaryStorage))
                yield return new ValidationResult($"{nameof(FolderSettings)}.{nameof(TemporaryStorage)} is not configured!",
                    new[] { nameof(TemporaryStorage) });
            if (string.IsNullOrEmpty(Archive))
                yield return new ValidationResult($"{nameof(FolderSettings)}.{nameof(Archive)} is not configured!",
                    new[] { nameof(Archive) });
            if (string.IsNullOrEmpty(StatisticsOutput)) // New validation
                yield return new ValidationResult($"{nameof(FolderSettings)}.{nameof(StatisticsOutput)} is not configured!",
                    new[] { nameof(StatisticsOutput) });
            if (string.IsNullOrEmpty(CameraStatisticsOutput))
                yield return new ValidationResult($"{nameof(FolderSettings)}.{nameof(CameraStatisticsOutput)} is not configured!",
                    new[] { nameof(CameraStatisticsOutput) });
            if (string.IsNullOrEmpty(Duplicates)) // Validation for duplicates folder
                yield return new ValidationResult($"{nameof(FolderSettings)}.{nameof(Duplicates)} is not configured!",
                    new[] { nameof(Duplicates) });
        }
    }
}
