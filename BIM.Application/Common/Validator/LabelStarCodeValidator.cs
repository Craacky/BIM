using BIM.Application.Models;
using FluentValidation;

namespace BIM.Application.Common.Validator
{
    public class LabelStarCodeValidator : AbstractValidator<LabelStarCodeVM>
    {
        public LabelStarCodeValidator()
        {
            RuleFor(q => q.Code)
                .NotEmpty().WithMessage("Label Star код не может быть пустым!");
        }
        public Func<object, string, Task<IEnumerable<string>>> ValueValidate => async (model, property) =>
        {
            var result = await ValidateAsync(ValidationContext<LabelStarCodeVM>
                                .CreateWithOptions((LabelStarCodeVM)model, q => q.IncludeProperties(property)));
            if (result.IsValid)
                return Array.Empty<string>();
            return result.Errors.Select(q => q.ErrorMessage);
        };
    }
}
