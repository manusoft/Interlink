namespace Interlink;

/// <summary>
/// Pipeline behavior that cancels a request if it exceeds the configured timeout.
/// Registered automatically when <see cref="InterlinkOptions.DefaultRequestTimeout"/> is set.
/// </summary>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public sealed class TimeoutBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutBehavior{TRequest,TResponse}"/> class.
    /// </summary>
    /// <param name="options">Interlink options that supply the default timeout.</param>
    public TimeoutBehavior(InterlinkOptions options)
    {
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        _timeout = options.DefaultRequestTimeout ?? Timeout.InfiniteTimeSpan;
    }

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_timeout <= TimeSpan.Zero || _timeout == Timeout.InfiniteTimeSpan)
            return await next(cancellationToken).ConfigureAwait(false);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        try
        {
            return await next(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Handling of '{typeof(TRequest).Name}' timed out after {_timeout.TotalMilliseconds:0} ms.");
        }
    }
}
