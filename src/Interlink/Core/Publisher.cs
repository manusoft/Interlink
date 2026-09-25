using Interlink.Contracts;
using System.Reflection;

namespace Interlink;

/// <summary>
/// Default implementation of <see cref="IPublisher"/> that resolves notification handlers,
/// runs notification pipeline behaviors, and invokes handlers using the configured
/// <see cref="PublishStrategy"/>.
/// </summary>
internal sealed class Publisher : IPublisher
{
    private readonly IServiceProvider _provider;
    private readonly Func<Type, object?> _serviceFactory;
    private readonly PublishStrategy _publishStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="Publisher"/> class.
    /// </summary>
    public Publisher(
        IServiceProvider provider,
        InterlinkOptions options,
        Func<Type, object?>? customFactory = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        _serviceFactory = customFactory ?? (type => _provider.GetService(type));
        _publishStrategy = options.PublishStrategy;
    }

    /// <inheritdoc />
    public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        if (notification is null)
            throw new ArgumentNullException(nameof(notification));

        var notificationType = notification.GetType();
        var handlerType = typeof(INotificationHandler<>).MakeGenericType(notificationType);
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(handlerType);

        var handlers = (_serviceFactory(enumerableType) as IEnumerable<object>)?.ToList()
                       ?? new List<object>();

        // Innermost: invoke all handlers according to strategy
        NotificationHandlerDelegate pipeline = token => InvokeHandlersAsync(handlers, notification, token);

        // Notification pipeline behaviors (ordered, outermost first)
        var behaviorType = typeof(INotificationPipelineBehavior<>).MakeGenericType(notificationType);
        var behaviors = ResolveEnumerable(behaviorType)
            .Select(b => (Instance: b, Order: GetOrder(b.GetType())))
            .OrderBy(x => x.Order)
            .Select(x => x.Instance)
            .ToList();

        for (var i = behaviors.Count - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var next = pipeline;
            pipeline = token => ((dynamic)behavior).Handle((dynamic)notification, next, token);
        }

        await pipeline(cancellationToken).ConfigureAwait(false);
    }

    private async Task InvokeHandlersAsync(
        List<object> handlers,
        object notification,
        CancellationToken cancellationToken)
    {
        if (handlers.Count == 0)
            return;

        if (_publishStrategy == PublishStrategy.Parallel)
        {
            var tasks = new Task[handlers.Count];
            for (var i = 0; i < handlers.Count; i++)
            {
                dynamic handler = handlers[i];
                tasks[i] = handler.Handle((dynamic)notification, cancellationToken);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return;
        }

        // Sequential (default)
        foreach (var handler in handlers)
        {
            await ((dynamic)handler).Handle((dynamic)notification, cancellationToken).ConfigureAwait(false);
        }
    }

    private IEnumerable<object> ResolveEnumerable(Type elementType)
    {
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
        var resolved = _serviceFactory(enumerableType);
        return resolved as IEnumerable<object> ?? Array.Empty<object>();
    }

    private static int GetOrder(Type type)
    {
        var attr = type.GetCustomAttribute<PipelineOrderAttribute>(inherit: false);
        return attr?.Order ?? int.MaxValue;
    }
}
