using FluentValidation;
using MediatR;

namespace BIM.Application.Common.Behaviour
{
    public class ValidateBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
            where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> validator;

        public ValidateBehaviour(IEnumerable<IValidator<TRequest>> _validator)
        {
            validator = _validator;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> reqDel, CancellationToken token)
        {
            var context = new ValidationContext<TRequest>(request);
            var fail = validator.Select(val => val.Validate(context))
                                .SelectMany(res => res.Errors)
                                .Where(f => f is not null)
                                .ToList();
            if (fail.Any())
                throw new ValidationException(fail);
            return await reqDel().ConfigureAwait(false);
        }
    }
}
