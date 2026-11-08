using Interlink.Contracts;
using System.Reflection;

namespace Interlink;

/// <summary>
/// Default implementation of <see cref="IPublisher"/> that resolves notification handlers,
/// runs notification pipeline behaviors, invokes handlers using the configured
/// <see cref="PublishStrategy"/>, and applies <see cref="NotificationExceptionStrategy"/>.
/// </summary>
internal sealed class Publisher : IPublisher
{
    private readonly IServiceProvider _provider;
    private readonly Func<Type, object?> _serviceFactory;
    private readonly PublishStrategy _publishStrategy;
    private readonly NotificationExceptionStrategy _exceptionStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="Publisher"/> class.
    /// </summary>
    public Publisher(IServiceProvider provider,
                     InterlinkOptions options,
                     Func<Type, object?>? customFactory = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        _serviceFactory = customFactory ?? (type => _provider.GetService(type));
        _publishStrategy = options.PublishStrategy;
        _exceptionStrategy = options.NotificationExceptionStrategy;
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

        NotificationHandlerDelegate pipeline = token =>
            InvokeHandlersAsync(handlers, notification, notificationType, token);

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

    private async Task InvokeHandlersAsync(List<object> handlers,
                                           object notification,
                                           Type notificationType,
                                           CancellationToken cancellationToken)
    {
        if (handlers.Count == 0)
            return;

        if (_publishStrategy == PublishStrategy.Parallel)
            await InvokeParallelAsync(handlers, notification, notificationType, cancellationToken).ConfigureAwait(false);
        else
            await InvokeSequentialAsync(handlers, notification, notificationType, cancellationToken).ConfigureAwait(false);
    }

    private async Task InvokeSequentialAsync(List<object> handlers,
                                             object notification,
                                             Type notificationType,
                                             CancellationToken cancellationToken)
    {
        var failures = new List<Exception>();

        foreach (var handler in handlers)
        {
            try
            {
                await ((dynamic)handler).Handle((dynamic)notification, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || cancellationToken.IsCancellationRequested)
            {
                if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                    throw;

                var wrapped = Wrap(notificationType, handler.GetType(), ex);
                await DispatchExceptionHandlersAsync(notification, wrapped, cancellationToken).ConfigureAwait(false);

                switch (_exceptionStrategy)
                {
                    case NotificationExceptionStrategy.StopOnFirst:
                        throw wrapped;
                    case NotificationExceptionStrategy.Aggregate:
                    case NotificationExceptionStrategy.Continue:
                        failures.Add(wrapped);
                        break;
                }
            }
        }

        ThrowIfAggregated(failures);
    }

    private async Task InvokeParallelAsync(List<object> handlers,
                                           object notification,
                                           Type notificationType,
                                           CancellationToken cancellationToken)
    {
        var tasks = handlers.Select(async handler =>
        {
            try
            {
                await ((dynamic)handler).Handle((dynamic)notification, cancellationToken).ConfigureAwait(false);
                return (Exception?)null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                    throw;

                var wrapped = Wrap(notificationType, handler.GetType(), ex);
                await DispatchExceptionHandlersAsync(notification, wrapped, cancellationToken).ConfigureAwait(false);
                return wrapped;
            }
        }).ToArray();

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var failures = results.Where(e => e is not null).Cast<Exception>().ToList();

        if (failures.Count == 0)
            return;

        if (_exceptionStrategy == NotificationExceptionStrategy.StopOnFirst)
            throw failures[0];

        if (_exceptionStrategy == NotificationExceptionStrategy.Aggregate)
            throw new AggregateException("One or more notification handlers failed.", failures);

        // Continue: already dispatched to exception handlers; do not rethrow
    }

    private void ThrowIfAggregated(List<Exception> failures)
    {
        if (failures.Count == 0)
            return;

        if (_exceptionStrategy == NotificationExceptionStrategy.Aggregate)
            throw new AggregateException("One or more notification handlers failed.", failures);

        // Continue: swallow after exception handlers ran
    }

    private static NotificationHandlerException Wrap(Type notificationType, Type handlerType, Exception ex)
        => ex is NotificationHandlerException nhe
            ? nhe
            : new NotificationHandlerException(notificationType, handlerType, ex);

    private async Task DispatchExceptionHandlersAsync(object notification,
                                                      NotificationHandlerException exception,
                                                      CancellationToken cancellationToken)
    {
        var notificationType = exception.NotificationType;
        var inner = exception.InnerException ?? exception;

        // Resolve INotificationExceptionHandler<TNotification, TException> for the concrete exception type
        // and walk the exception inheritance chain so handlers for Exception also match.
        var exceptionType = inner.GetType();
        while (true)
        {
            var handlerInterface = typeof(INotificationExceptionHandler<,>)
                .MakeGenericType(notificationType, exceptionType);

            var enumerableType = typeof(IEnumerable<>).MakeGenericType(handlerInterface);
            var handlers = _serviceFactory(enumerableType) as IEnumerable<object> ?? Array.Empty<object>();

            foreach (var handler in handlers)
            {
                await ((dynamic)handler).Handle((dynamic)notification, (dynamic)inner, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (exceptionType == typeof(Exception))
                break;

            exceptionType = exceptionType.BaseType ?? typeof(Exception);
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
