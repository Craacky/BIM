using BIM.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BIM.Application.Common.Behaviour
{
    public class PerformanceBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest<TResponse>
    {
        private readonly ICurrentUserService userService;
        private readonly ILogger<TRequest> logger;
        private readonly Stopwatch watch;

        public PerformanceBehaviour(ICurrentUserService _userService, ILogger<TRequest> _logger)
        {
            userService = _userService;
            logger = _logger;
            watch = new();
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            watch.Start();
            var response = await next().ConfigureAwait(false);
            watch.Stop();
            var ellapsed = watch.ElapsedMilliseconds;
            if (ellapsed > 500)
            {
                var requestName = typeof(TRequest).Name;
                var userName = userService?.UserName ?? "Unknown";
                logger.LogWarning($"{requestName} long running request (ms - {ellapsed}) with {request}, {userName}");
            }
            return response;
        }
    }
}
