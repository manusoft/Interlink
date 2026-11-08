using Interlink.Contracts;

namespace Interlink;

/// <summary>
/// Handles an exception thrown by a notification handler for <typeparamref name="TNotification"/>.
/// Use this to log, write an outbox/failure record, or compensate without failing the whole publish.
/// </summary>
/// <typeparam name="TNotification">The notification type.</typeparam>
/// <typeparam name="TException">The exception type this handler deals with.</typeparam>
public interface INotificationExceptionHandler<in TNotification, in TException>
    where TNotification : INotification
    where TException : Exception
{
    /// <summary>
    /// Called when a notification handler throws <typeparamref name="TException"/>.
    /// </summary>
    /// <param name="notification">The notification being published.</param>
    /// <param name="exception">The exception thrown by a handler.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Handle(TNotification notification, TException exception, CancellationToken cancellationToken);
}
