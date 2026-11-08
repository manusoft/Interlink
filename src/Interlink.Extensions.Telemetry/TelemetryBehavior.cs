using System.Diagnostics;

namespace Interlink.Extensions.Telemetry;

/// <summary>
/// Request pipeline behavior that creates an <see cref="Activity"/> for each <c>Send</c>.
/// </summary>
public sealed class TelemetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).FullName ?? typeof(TRequest).Name;
        using var activity = InterlinkActivitySource.Source.StartActivity(
            $"Interlink.Send {typeof(TRequest).Name}",
            ActivityKind.Internal);

        activity?.SetTag("interlink.request_type", requestName);
        activity?.SetTag("interlink.operation", "send");

        try
        {
            var response = await next(cancellationToken).ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return response;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);
            activity?.SetTag("exception.message", ex.Message);
            throw;
        }
    }
}
