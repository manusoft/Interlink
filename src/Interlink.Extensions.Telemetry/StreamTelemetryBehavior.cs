using System.Diagnostics;
using System.Runtime.CompilerServices;
using Interlink.Contracts;

namespace Interlink.Extensions.Telemetry;

/// <summary>
/// Stream pipeline behavior that creates an <see cref="Activity"/> for each <c>CreateStream</c>.
/// </summary>
public sealed class StreamTelemetryBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <inheritdoc />
    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).FullName ?? typeof(TRequest).Name;
        using var activity = InterlinkActivitySource.Source.StartActivity(
            $"Interlink.CreateStream {typeof(TRequest).Name}",
            ActivityKind.Internal);

        activity?.SetTag("interlink.request_type", requestName);
        activity?.SetTag("interlink.operation", "create_stream");

        var count = 0;
        Exception? error = null;
        try
        {
            await foreach (var item in next().WithCancellation(cancellationToken))
            {
                count++;
                yield return item;
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        finally
        {
            activity?.SetTag("interlink.stream_items", count);
            if (error is not null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, error.Message);
            }
        }
    }
}
