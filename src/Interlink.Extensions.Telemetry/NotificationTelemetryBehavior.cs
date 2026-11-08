using System.Diagnostics;
using Interlink.Contracts;

namespace Interlink.Extensions.Telemetry;

/// <summary>
/// Notification pipeline behavior that creates an <see cref="Activity"/> for each <c>Publish</c>.
/// </summary>
public sealed class NotificationTelemetryBehavior<TNotification> : INotificationPipelineBehavior<TNotification>
    where TNotification : INotification
{
    /// <inheritdoc />
    public async Task Handle(
        TNotification notification,
        NotificationHandlerDelegate next,
        CancellationToken cancellationToken)
    {
        var name = typeof(TNotification).FullName ?? typeof(TNotification).Name;
        using var activity = InterlinkActivitySource.Source.StartActivity(
            $"Interlink.Publish {typeof(TNotification).Name}",
            ActivityKind.Internal);

        activity?.SetTag("interlink.notification_type", name);
        activity?.SetTag("interlink.operation", "publish");

        try
        {
            await next(cancellationToken).ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);
            activity?.SetTag("exception.message", ex.Message);
            throw;
        }
    }
}
