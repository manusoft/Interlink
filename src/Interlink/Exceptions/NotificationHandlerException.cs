namespace Interlink;

/// <summary>
/// Wraps an exception thrown by a single notification handler, including
/// the notification and handler types for diagnostics and outbox-style recovery.
/// </summary>
public sealed class NotificationHandlerException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationHandlerException"/> class.
    /// </summary>
    public NotificationHandlerException(Type notificationType, Type handlerType, Exception innerException)
        : base(
            $"Notification handler '{handlerType.FullName}' failed while handling '{notificationType.FullName}'.",
            innerException)
    {
        NotificationType = notificationType ?? throw new ArgumentNullException(nameof(notificationType));
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
    }

    /// <summary>The notification type that was being published.</summary>
    public Type NotificationType { get; }

    /// <summary>The handler type that threw.</summary>
    public Type HandlerType { get; }
}
