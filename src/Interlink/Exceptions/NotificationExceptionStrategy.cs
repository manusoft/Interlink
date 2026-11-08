namespace Interlink;

/// <summary>
/// Controls how exceptions thrown by notification handlers are treated during <see cref="IPublisher.Publish{TNotification}"/>.
/// </summary>
public enum NotificationExceptionStrategy
{
    /// <summary>
    /// Stop at the first handler failure and rethrow that exception.
    /// Remaining handlers are not invoked (sequential) or may still be in-flight (parallel).
    /// This is the default.
    /// </summary>
    StopOnFirst = 0,

    /// <summary>
    /// Invoke every handler. If one or more fail, throw a single
    /// <see cref="AggregateException"/> after all handlers have completed.
    /// </summary>
    Aggregate = 1,

    /// <summary>
    /// Invoke every handler. Failures are passed to any registered
    /// <see cref="INotificationExceptionHandler{TNotification,TException}"/> instances.
    /// If no exception handler handles the error, the exception is swallowed
    /// (use with care — prefer Aggregate or explicit exception handlers).
    /// </summary>
    Continue = 2
}
