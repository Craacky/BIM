using BIM.Application.Common.Safety;
using FluentValidation;

namespace BIM.Application.Common.Validator
{
    public class LoginVMValidator : AbstractValidator<LoginViewModel>
    {
        public LoginVMValidator()
        {
            RuleFor(q => q.UserName)
                .NotEmpty()
                .Length(2, 20);
            RuleFor(q => q.Password)
                .NotEmpty().WithMessage("Пароль не может быть пустым!")
                .MinimumLength(4).WithMessage("Длина пароля должна быть минимум 4 символа!")
                .Matches(@"[0-9]+").WithMessage("Пароль должен содержать только числовые значения!");
        }

        public Func<object, string, Task<IEnumerable<string>>> ValueValidate => async (model, property) =>
        {
            var result = await ValidateAsync(ValidationContext<LoginViewModel>
                                .CreateWithOptions((LoginViewModel)model, q => q.IncludeProperties(property)));
            if (result.IsValid)
                return Array.Empty<string>();
            return result.Errors.Select(q => q.ErrorMessage);
        };
    }
}