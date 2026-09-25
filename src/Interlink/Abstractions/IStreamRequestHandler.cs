using Interlink.Contracts;

namespace Interlink;

/// <summary>
/// Defines a handler for a streaming request of type <typeparamref name="TRequest"/>
/// that yields a sequence of <typeparamref name="TResponse"/> values.
/// </summary>
/// <typeparam name="TRequest">The type of the stream request.</typeparam>
/// <typeparam name="TResponse">The type of each streamed response item.</typeparam>
public interface IStreamRequestHandler<in TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Handles the stream request and yields response items asynchronously.
    /// </summary>
    /// <param name="request">The request instance to handle.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An asynchronous stream of response items.</returns>
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
