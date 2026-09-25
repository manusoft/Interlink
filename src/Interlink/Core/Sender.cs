using Interlink.Contracts;
using System.Reflection;

namespace Interlink;

/// <summary>
/// Default implementation of <see cref="ISender"/> that resolves handlers,
/// runs pre-processors, pipeline behaviors (ordered), the handler itself,
/// and post-processors.
/// </summary>
internal sealed class Sender : ISender
{
    private readonly IServiceProvider _provider;
    private readonly Func<Type, object?> _serviceFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="Sender"/> class.
    /// </summary>
    /// <param name="provider">The service provider used for resolution when no custom factory is supplied.</param>
    /// <param name="customFactory">
    /// Optional custom factory. When provided it is used instead of <paramref name="provider"/>
    /// for resolving handlers, behaviors, and processors.
    /// </param>
    public Sender(IServiceProvider provider, Func<Type, object?>? customFactory = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _serviceFactory = customFactory ?? (type => _provider.GetService(type));
    }

    /// <inheritdoc />
    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var requestType = request.GetType();
        var responseType = typeof(TResponse);

        // Resolve main handler
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
        var handler = ResolveRequired(handlerType, requestType);

        // Pre-processors (run before pipeline)
        var preProcessorType = typeof(IRequestPreProcessor<>).MakeGenericType(requestType);
        var preProcessors = ResolveEnumerable(preProcessorType);

        foreach (var processor in preProcessors)
        {
            await ((dynamic)processor).Process((dynamic)request, cancellationToken).ConfigureAwait(false);
        }

        // Build the innermost handler delegate
        RequestHandlerDelegate<TResponse> pipeline = ct =>
            ((dynamic)handler).Handle((dynamic)request, ct);

        // Pipeline behaviors – ordered by PipelineOrderAttribute (or explicit registration order)
        var behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
        var behaviors = ResolveEnumerable(behaviorType)
            .Select(b => (Instance: b, Order: GetOrder(b.GetType())))
            .OrderBy(x => x.Order)
            .Select(x => x.Instance)
            .ToList();

        // Wrap in reverse so that the lowest-order behavior becomes the outermost
        for (var i = behaviors.Count - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var next = pipeline;
            pipeline = ct => ((dynamic)behavior).Handle((dynamic)request, next, ct);
        }

        // Execute the full pipeline
        TResponse response = await pipeline(cancellationToken).ConfigureAwait(false);

        // Post-processors (run after successful pipeline)
        var postProcessorType = typeof(IRequestPostProcessor<,>).MakeGenericType(requestType, responseType);
        var postProcessors = ResolveEnumerable(postProcessorType);

        foreach (var processor in postProcessors)
        {
            await ((dynamic)processor).Process((dynamic)request, (dynamic)response!, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    /// <inheritdoc />
    public Task Send(IRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return Send<Unit>(request, cancellationToken);
    }


    /// <inheritdoc />
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var requestType = request.GetType();
        var responseType = typeof(TResponse);

        var handlerType = typeof(IStreamRequestHandler<,>).MakeGenericType(requestType, responseType);
        dynamic handler = ResolveRequired(handlerType, requestType);

        // Handler
        StreamHandlerDelegate<TResponse> pipeline = () =>
            ((dynamic)handler).Handle((dynamic)request, cancellationToken);

        var behaviorType = typeof(IStreamPipelineBehavior<,>).MakeGenericType(requestType, responseType);
        var behaviors = ResolveEnumerable(behaviorType)
            .Select(b => (Instance: b, Order: GetOrder(b.GetType())))
            .OrderBy(x => x.Order)
            .Select(x => x.Instance)
            .ToList();

        // Behaviors
        for (var i = behaviors.Count - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var next = pipeline;
            pipeline = () => ((dynamic)behavior).Handle((dynamic)request, next, cancellationToken);
        }

        return pipeline();
    }

    private object ResolveRequired(Type serviceType, Type requestType)
    {
        var instance = _serviceFactory(serviceType);
        if (instance is null)
            throw new HandlerNotFoundException(requestType, serviceType);
        return instance;
    }

    private IEnumerable<object> ResolveEnumerable(Type elementType)
    {
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
        var resolved = _serviceFactory(enumerableType);
        return resolved as IEnumerable<object> ?? Array.Empty<object>();
    }

    private static int GetOrder(Type behaviorType)
    {
        var attr = behaviorType.GetCustomAttribute<PipelineOrderAttribute>(inherit: false);
        return attr?.Order ?? int.MaxValue;
    }
}
