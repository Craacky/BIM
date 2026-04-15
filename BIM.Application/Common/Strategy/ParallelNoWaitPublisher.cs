using MediatR;

namespace BIM.Application.Common.Strategy
{
    public class ParallelNoWaitPublisher : INotificationPublisher
    {
        public Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
        {
            foreach (var item in handlerExecutors)
                Task.Run(() => item.HandlerCallback(notification, cancellationToken));
            return Task.CompletedTask;
        }
    }
}
