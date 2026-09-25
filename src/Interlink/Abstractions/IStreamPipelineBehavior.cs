using Interlink.Contracts;

namespace Interlink;

/// <summary>
/// Represents a delegate that continues the stream request pipeline.
/// </summary>
/// <typeparam name="TResponse">The type of each streamed response item.</typeparam>
/// <returns>An asynchronous stream of response items.</returns>
public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<TResponse>();

/// <summary>
/// Defines a behavior in the stream request pipeline that can inspect or wrap
/// streaming before and/or after the next component.
/// </summary>
/// <typeparam name="TRequest">The type of the stream request.</typeparam>
/// <typeparam name="TResponse">The type of each streamed response item.</typeparam>
public interface IStreamPipelineBehavior<in TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Handles the stream request and optionally calls the next delegate in the pipeline.
    /// </summary>
    /// <param name="request">The request to process.</param>
    /// <param name="next">The next delegate in the pipeline.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An asynchronous stream of response items.</returns>
    IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
