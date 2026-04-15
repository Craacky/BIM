using BIM.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BIM.Application.Common.Behaviour
{
    public class UnhandledExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        private readonly ICurrentUserService userService;
        private readonly ILogger<TRequest> logger;

        public UnhandledExceptionBehaviour(ICurrentUserService _userService, ILogger<TRequest> _logger)
        {
            userService = _userService;
            logger = _logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            try
            {
                return await next().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var requestName = typeof(TRequest).Name;
                var userName = userService?.UserName ?? "Unknown";
                logger.LogError(ex, $"{requestName} : {ex.Message} with {request} by {userName}");
                throw;
            }
        }
    }
}
