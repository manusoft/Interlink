using Interlink.Contracts;

namespace Interlink;

/// <summary>
/// Defines a sender that dispatches requests to their corresponding handlers and returns responses.
/// </summary>
public interface ISender
{
    /// <summary>
    /// Sends a request and returns the response produced by its handler.
    /// </summary>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a void/command request that does not produce a response value.
    /// </summary>
    Task Send(IRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a stream request and returns an asynchronous stream of responses.
    /// </summary>
    /// <typeparam name="TResponse">The type of each item in the response stream.</typeparam>
    /// <param name="request">The stream request to send.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An asynchronous stream of response items.</returns>
    /// <exception cref="HandlerNotFoundException">Thrown when no stream handler is registered for the request type.</exception>
    IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);
}
