using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Interlink;

/// <summary>
/// Extension methods for registering Interlink services with an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Interlink core services, scans the supplied assemblies for handlers,
    /// pre/post processors, and registers any explicitly configured pipeline behaviors.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">
    /// Optional configuration callback used to register open-generic pipeline behaviors,
    /// publish strategy, timeouts, and to supply a custom service factory.
    /// </param>
    /// <param name="assemblies">
    /// Assemblies to scan for handlers and processors.
    /// When omitted, the calling assembly is scanned.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> instance so that calls can be chained.</returns>
    public static IServiceCollection AddInterlink(
        this IServiceCollection services,
        Action<InterlinkOptions>? configure = null,
        params Assembly[] assemblies)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        var options = new InterlinkOptions();
        configure?.Invoke(options);

        // Make options available to TimeoutBehavior and Publisher
        services.AddSingleton(options);

        if (assemblies is null || assemblies.Length == 0)
            assemblies = new[] { Assembly.GetCallingAssembly() };

        // Request pipeline behaviors
        foreach (var (behaviorType, _) in options.OpenBehaviors)
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), behaviorType);
        }

        // Notification pipeline behaviors
        foreach (var (behaviorType, _) in options.OpenNotificationBehaviors)
        {
            services.AddScoped(typeof(INotificationPipelineBehavior<>), behaviorType);
        }

        // Stream pipeline behaviors
        foreach (var (behaviorType, _) in options.OpenStreamBehaviors)
        {
            services.AddScoped(typeof(IStreamPipelineBehavior<,>), behaviorType);
        }

        // Built-in timeout behavior when configured
        if (options.DefaultRequestTimeout is { } timeout && timeout > TimeSpan.Zero)
        {
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TimeoutBehavior<,>));
        }

        // Scan assemblies for handlers and processors
        foreach (var assembly in assemblies)
        {
            RegisterClosedGenericImplementations(services, assembly, typeof(IRequestHandler<,>));
            RegisterClosedGenericImplementations(services, assembly, typeof(INotificationHandler<>));
            RegisterClosedGenericImplementations(services, assembly, typeof(IRequestPreProcessor<>));
            RegisterClosedGenericImplementations(services, assembly, typeof(IRequestPostProcessor<,>));
            RegisterClosedGenericImplementations(services, assembly, typeof(INotificationPipelineBehavior<>));
            RegisterClosedGenericImplementations(services, assembly, typeof(IStreamRequestHandler<,>));
            RegisterClosedGenericImplementations(services, assembly, typeof(IStreamPipelineBehavior<,>));
        }

        var factory = options.ServiceFactory;

        if (factory is not null)
        {
            services.AddScoped<ISender>(sp => new Sender(sp, factory));
            services.AddScoped<IPublisher>(sp => new Publisher(sp, options, factory));
        }
        else
        {
            services.AddScoped<ISender, Sender>();
            services.AddScoped<IPublisher>(sp => new Publisher(sp, options));
        }

        services.AddScoped<IMediator, Mediator>();

        return services;
    }

    private static void RegisterClosedGenericImplementations(
        IServiceCollection services,
        Assembly assembly,
        Type openGenericType)
    {
        foreach (var (serviceType, implementationType) in TypeScanner.Scan(assembly, openGenericType))
        {
            services.AddScoped(serviceType, implementationType);
        }
    }
}
