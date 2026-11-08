# Changelog

All notable changes to **Interlink** are documented in this file.

## [1.7.0] - 2026-11-08

### Added

- **NotificationExceptionStrategy** on `InterlinkOptions`:
  - `StopOnFirst` (default) — rethrow the first handler failure
  - `Aggregate` — run all handlers, then throw `AggregateException` if any failed
  - `Continue` — run all handlers; failures handled by exception handlers and not rethrown by default
- **`INotificationExceptionHandler<TNotification, TException>`** — handle per-handler failures (logging, outbox, compensation)
- **`NotificationHandlerException`** — wraps a single handler failure with `NotificationType` and `HandlerType`
- **`Interlink.Extensions.Telemetry`** — Activity instrumentation for:
  - `Send` → `Interlink.Send {Request}`
  - `Publish` → `Interlink.Publish {Notification}`
  - `CreateStream` → `Interlink.CreateStream {Request}`
- Registration: `services.AddInterlinkTelemetry()`
- Activity source name: **`Interlink`**
- Unit tests for Aggregate, Continue, StopOnFirst, and exception handler invocation

### Changed

- `Publisher` applies `NotificationExceptionStrategy` and dispatches notification exception handlers before rethrowing or continuing
- Assembly scanning registers `INotificationExceptionHandler<,>` implementations

### Packages

| Package | Version |
|---------|---------|
| Interlink | 1.7.0 |
| Interlink.Extensions.Telemetry | 1.7.0 |
| Interlink.Extensions.Logging | 1.7.0 |
| Interlink.Extensions.Validation | 1.7.0 |
| Interlink.AspNetCore | 1.7.0 |
| Interlink.Analyzers | 1.7.0 |

---

## [1.6.0]

### Added

- **Streaming requests** — `IStreamRequest<T>`, `IStreamRequestHandler<TRequest,TResponse>`, `ISender.CreateStream` / `IMediator.CreateStream`
- **Stream pipeline behaviors** — `IStreamPipelineBehavior<TRequest,TResponse>`, `AddStreamBehavior`
- **Publish strategies** — `PublishStrategy.Sequential` (default) and `PublishStrategy.Parallel`
- **Notification pipeline behaviors** — `INotificationPipelineBehavior<T>`, `AddNotificationBehavior`
- **Request timeout** — `InterlinkOptions.DefaultRequestTimeout` + built-in `TimeoutBehavior<,>`
- **ILINK002** analyzer — duplicate request handlers

### Changed

- `Publisher` supports publish strategies and notification pipelines
- Analyzer detects both `IRequestHandler<TRequest>` and `IRequestHandler<TRequest,TResponse>`

---

## [1.5.2]

### Added

- Unified **`IMediator`** interface (composes `ISender` + `IPublisher`)

---

## [1.5.1]

### Added

- **`Unit`** and non-generic **`IRequest`** / **`IRequestHandler<TRequest>`** for void-style commands
- **`ISender.Send(IRequest)`** convenience overload

### Fixed

- Analyzer packaging (`build/Interlink.Analyzers.props`) for reliable NuGet consumption

---

## [1.5.0]

### Added

- `Interlink.Extensions.Logging`
- `Interlink.Extensions.Validation` (FluentValidation)
- `Interlink.AspNetCore` (exception filter → ProblemDetails)
- `Interlink.Analyzers` (**ILINK001** missing handler)
- `HandlerNotFoundException`
- Pipeline ordering via `PipelineOrderAttribute` / `AddBehavior(..., order)`

---

## [1.4.x] and earlier

- Core mediator: request/response, notifications, pipeline behaviors, pre/post processors, DI scanning
