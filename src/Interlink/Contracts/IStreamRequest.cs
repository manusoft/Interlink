namespace Interlink.Contracts;

/// <summary>
/// Marker interface for a request that produces a stream of <typeparamref name="TResponse"/> values.
/// </summary>
/// <typeparam name="TResponse">The type of each item in the response stream.</typeparam>
public interface IStreamRequest<out TResponse>
{
}
