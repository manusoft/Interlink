namespace Interlink;

/// <summary>
/// Specifies how notification handlers are invoked when a notification is published.
/// </summary>
public enum PublishStrategy
{
    /// <summary>
    /// Handlers are invoked one after another in registration order.
    /// This is the default and preserves ordering guarantees.
    /// </summary>
    Sequential = 0,

    /// <summary>
    /// Handlers are invoked concurrently. Completes when all handlers finish.
    /// Exceptions from any handler are aggregated into an <see cref="AggregateException"/>.
    /// </summary>
    Parallel = 1
}
