namespace Interlink;

/// <summary>
/// Provides configuration options for Interlink services.
/// </summary>
public class InterlinkOptions
{
    internal List<(Type Type, int? Order)> OpenBehaviors { get; } = new();

    internal List<(Type Type, int? Order)> OpenNotificationBehaviors { get; } = new();

    internal List<(Type Type, int? Order)> OpenStreamBehaviors { get; } = new();

    /// <summary>
    /// Gets or sets an optional custom factory used to resolve handlers and pipeline components.
    /// When set, this factory is preferred over the default <see cref="IServiceProvider"/>.
    /// </summary>
    public Func<Type, object?>? ServiceFactory { get; set; }

    /// <summary>
    /// Gets or sets how notification handlers are invoked when publishing.
    /// Default is <see cref="PublishStrategy.Sequential"/>.
    /// </summary>
    public PublishStrategy PublishStrategy { get; set; } = PublishStrategy.Sequential;

    /// <summary>
    /// Gets or sets an optional default timeout applied to all requests via
    /// <see cref="TimeoutBehavior{TRequest,TResponse}"/>.
    /// When null, no timeout behavior is registered.
    /// </summary>
    public TimeSpan? DefaultRequestTimeout { get; set; }

    /// <summary>
    /// Adds an open-generic request pipeline behavior type to the configuration.
    /// </summary>
    public void AddBehavior(Type openGenericBehaviorType, int? order = null)
    {
        if (openGenericBehaviorType is null)
            throw new ArgumentNullException(nameof(openGenericBehaviorType));

        if (!openGenericBehaviorType.IsGenericTypeDefinition ||
            openGenericBehaviorType.GetGenericArguments().Length != 2)
        {
            throw new ArgumentException(
                "Behavior must be an open generic type definition with exactly two generic parameters " +
                "(for example typeof(MyBehavior<,>)).",
                nameof(openGenericBehaviorType));
        }

        OpenBehaviors.Add((openGenericBehaviorType, order));
    }

    /// <summary>
    /// Adds an open-generic request pipeline behavior using a generic type parameter.
    /// </summary>
    public void AddBehavior<TBehavior>(int? order = null)
        where TBehavior : class
    {
        AddBehavior(typeof(TBehavior), order);
    }

    /// <summary>
    /// Adds an open-generic notification pipeline behavior type to the configuration.
    /// </summary>
    public void AddNotificationBehavior(Type openGenericBehaviorType, int? order = null)
    {
        if (openGenericBehaviorType is null)
            throw new ArgumentNullException(nameof(openGenericBehaviorType));

        if (!openGenericBehaviorType.IsGenericTypeDefinition ||
            openGenericBehaviorType.GetGenericArguments().Length != 1)
        {
            throw new ArgumentException(
                "Notification behavior must be an open generic type definition with exactly one generic parameter " +
                "(for example typeof(MyNotificationBehavior<>)).",
                nameof(openGenericBehaviorType));
        }

        OpenNotificationBehaviors.Add((openGenericBehaviorType, order));
    }

    /// <summary>
    /// Adds an open-generic notification pipeline behavior using a generic type parameter.
    /// </summary>
    public void AddNotificationBehavior<TBehavior>(int? order = null)
        where TBehavior : class
    {
        AddNotificationBehavior(typeof(TBehavior), order);
    }

    /// <summary>
    /// Adds an open-generic stream pipeline behavior type to the configuration.
    /// </summary>
    public void AddStreamBehavior(Type openGenericBehaviorType, int? order = null)
    {
        if (openGenericBehaviorType is null)
            throw new ArgumentNullException(nameof(openGenericBehaviorType));

        if (!openGenericBehaviorType.IsGenericTypeDefinition ||
            openGenericBehaviorType.GetGenericArguments().Length != 2)
        {
            throw new ArgumentException(
                "Stream behavior must be an open generic type definition with exactly two generic parameters " +
                "(for example typeof(MyStreamBehavior<,>)).",
                nameof(openGenericBehaviorType));
        }

        OpenStreamBehaviors.Add((openGenericBehaviorType, order));
    }

    /// <summary>
    /// Adds an open-generic stream pipeline behavior using a generic type parameter.
    /// </summary>
    public void AddStreamBehavior<TBehavior>(int? order = null)
        where TBehavior : class
    {
        AddStreamBehavior(typeof(TBehavior), order);
    }
}
