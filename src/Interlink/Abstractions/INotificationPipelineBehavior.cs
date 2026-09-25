using Interlink.Contracts;

namespace Interlink;

/// <summary>
/// Represents a delegate that continues the notification pipeline.
/// </summary>
/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
/// <returns>A task that represents the asynchronous operation.</returns>
public delegate Task NotificationHandlerDelegate(CancellationToken cancellationToken = default);

/// <summary>
/// Defines a behavior in the notification pipeline that can inspect or wrap
/// the publishing of a notification before and/or after the handlers run.
/// </summary>
/// <typeparam name="TNotification">The type of the notification.</typeparam>
public interface INotificationPipelineBehavior<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles the notification and optionally calls the next delegate in the pipeline.
    /// </summary>
    /// <param name="notification">The notification being published.</param>
    /// <param name="next">The next delegate in the pipeline (remaining behaviors + handlers).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task Handle(TNotification notification, NotificationHandlerDelegate next, CancellationToken cancellationToken);
}
