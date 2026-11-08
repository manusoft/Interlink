using System.Diagnostics;

namespace Interlink.Extensions.Telemetry;

/// <summary>
/// Shared <see cref="ActivitySource"/> for Interlink instrumentation.
/// </summary>
public static class InterlinkActivitySource
{
    /// <summary>Activity source name used in OpenTelemetry configuration.</summary>
    public const string Name = "Interlink";

    /// <summary>The activity source instance.</summary>
    public static readonly ActivitySource Source = new(Name, "1.7.0");
}
