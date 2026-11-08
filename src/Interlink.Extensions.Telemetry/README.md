![Static Badge](https://img.shields.io/badge/Interlink.Extensions.Telemetry-blue)
![NuGet Version](https://img.shields.io/nuget/v/Interlink.Extensions.Telemetry)
![NuGet Downloads](https://img.shields.io/nuget/dt/Interlink.Extensions.Telemetry)

# Interlink.Extensions.Telemetry

Activity-based instrumentation for [Interlink](https://www.nuget.org/packages/Interlink).

## Install

```bash
dotnet add package Interlink.Extensions.Telemetry
```

## Usage

```csharp
builder.Services.AddInterlink(typeof(MyHandler).Assembly);
builder.Services.AddInterlinkTelemetry();
```

Subscribe to the activity source name **`Interlink`** in OpenTelemetry:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource(Interlink.Extensions.Telemetry.InterlinkActivitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter());
```

## Activities

| Operation | Activity name pattern | Tags |
|-----------|----------------------|------|
| `Send` | `Interlink.Send {Request}` | `interlink.request_type`, `interlink.operation` |
| `Publish` | `Interlink.Publish {Notification}` | `interlink.notification_type`, `interlink.operation` |
| `CreateStream` | `Interlink.CreateStream {Request}` | `interlink.request_type`, `interlink.stream_items` |

Failed operations set activity status to **Error** and add exception tags.

## License

MIT © ManuHub
