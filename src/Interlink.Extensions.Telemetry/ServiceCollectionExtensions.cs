using Microsoft.Extensions.DependencyInjection;

namespace Interlink.Extensions.Telemetry;

/// <summary>
/// Extension methods for registering Interlink telemetry behaviors.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers request, notification, and stream telemetry pipeline behaviors.
    /// Configure OpenTelemetry (or another listener) to subscribe to activity source
    /// <c>Interlink</c> (<see cref="InterlinkActivitySource.Name"/>).
    /// </summary>
    public static IServiceCollection AddInterlinkTelemetry(this IServiceCollection services)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TelemetryBehavior<,>));
        services.AddScoped(typeof(INotificationPipelineBehavior<>), typeof(NotificationTelemetryBehavior<>));
        services.AddScoped(typeof(IStreamPipelineBehavior<,>), typeof(StreamTelemetryBehavior<,>));

        return services;
    }
}
