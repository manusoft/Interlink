![Static Badge](https://img.shields.io/badge/Interlink-blue)
![NuGet Version](https://img.shields.io/nuget/v/Interlink)
![NuGet Downloads](https://img.shields.io/nuget/dt/Interlink)

# Interlink

**Interlink** is a lightweight and modern mediator library for .NET, designed to decouple your code through request/response and notification patterns. Built with simplicity and performance in mind, it helps streamline communication between components while maintaining a clean architecture.

---

## ✨ Features

* 🧩 Simple mediator pattern for request/response
* 🔁 Publish/Subscribe notification system
* ⚡ Notification publish strategies (sequential / parallel)
* 🔔 Notification pipeline behaviors
* ⏱️ Optional request timeout behavior
* 📡 Streaming requests (`IStreamRequest` / `CreateStream`)
* 🎯 Unified `IMediator` (combines `ISender` + `IPublisher`)
* ⚪ `Unit` support for fire-and-forget / void commands (`IRequest` / `IRequest<Unit>`)
* 🔧 Pipeline behaviors (logging, validation, etc.)
* 🧠 Clean separation of concerns via handlers
* 🪝 Dependency injection support out of the box
* 🔄 Pre and Post Processors for enhanced lifecycle control
* 🔍 Assembly scanning for automatic handler registration
* 🧪 Custom service factory injection
* 🔄 Pipeline ordering via attributes or configuration
* 🚨 Dedicated `HandlerNotFoundException`
* ✅ Compatible with .NET Standard 2.0+ to .NET 10
* 📦 Optional packages: Logging, FluentValidation, ASP.NET Core, Analyzer

---

## 📦 Installation

```bash
dotnet add package Interlink
```

Optional packages:

```bash
dotnet add package Interlink.Extensions.Logging
dotnet add package Interlink.Extensions.Validation
dotnet add package Interlink.AspNetCore
dotnet add package Interlink.Analyzers
```

---

## ⚙️ Setup

Register Interlink in `Program.cs` (or `Startup.cs`):

```csharp
builder.Services.AddInterlink();
```

Scan a specific assembly:

```csharp
builder.Services.AddInterlink(typeof(MyHandler).Assembly);
```

Configure pipeline behaviors and optional custom factory:

```csharp
builder.Services.AddInterlink(options =>
{
    // Open-generic behaviors (order is optional; lower runs first / outermost)
    options.AddBehavior(typeof(LoggingBehavior<,>), order: 0);
    options.AddBehavior(typeof(ValidationBehavior<,>), order: 1);

    // Optional custom resolution factory
    options.ServiceFactory = type => /* your custom resolver */;
}, typeof(MyHandler).Assembly);
```

With the extension packages:

```csharp
builder.Services.AddInterlink(typeof(MyHandler).Assembly);
builder.Services.AddInterlinkLogging();
builder.Services.AddInterlinkValidation(typeof(MyValidator).Assembly);
builder.Services.AddInterlinkAspNetCore(); // registers exception filter
```

---

## 📨 Request / Response Pattern

### 1. Define a request and handler

#### With response
```csharp
using Interlink;
using Interlink.Contracts;

public class GetAllPets
{
    public sealed record Query : IRequest<List<string>>;

    public sealed class Handler : IRequestHandler<Query, List<string>>
    {
        public Task<List<string>> Handle(Query request, CancellationToken cancellationToken)
        {
            var pets = new List<string> { "Dog", "Cat", "Fish" };
            return Task.FromResult(pets);
        }
    }
}
```

#### Without response (using Unit)
```csharp
using Interlink;
using Interlink.Contracts;

public class CreatePet
{
    public sealed record Command(string Name) : IRequest;   // or IRequest<Unit>

    public sealed class Handler : IRequestHandler<Command, Unit>
    {
        public Task<Unit> Handle(Command request, CancellationToken cancellationToken)
        {
            // Save pet to database...
            Console.WriteLine($"Pet '{request.Name}' created");

            return Unit.Value;          // or Task.FromResult(Unit.Value)
        }
    }
}
```

> **Note**  
> - `IRequest` is equivalent to `IRequest<Unit>`.  
> - Always return `Unit.Value` (or `Task.FromResult(Unit.Value)`) from handlers that produce no meaningful response.


### 2. Send the request

```csharp
[ApiController]
[Route("api/[controller]")]
public class PetController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllPets(CancellationToken cancellationToken)
    {
        var pets = await mediator.Send(new GetAllPets.Query(), cancellationToken);
        return Ok(pets);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePet(string name, CancellationToken cancellationToken)
    {
        await mediator.Send(new CreatePet.Command(name), cancellationToken);
        return NoContent();
    }

}
```

You can also inject `ISender` if you only need request/response functionality.

If no handler is registered, `Send` throws `HandlerNotFoundException`.

---

## 📣 Notifications (Publish / Subscribe)

### 1. Define a notification

```csharp
public sealed class UserCreated(string userName) : INotification
{
    public string UserName { get; } = userName;
}
```

### 2. Create one or more handlers

```csharp
public sealed class SendWelcomeEmail : INotificationHandler<UserCreated>
{
    public Task Handle(UserCreated notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Welcome email sent to {notification.UserName}");
        return Task.CompletedTask;
    }
}

public sealed class WriteAuditLog : INotificationHandler<UserCreated>
{
    public Task Handle(UserCreated notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Audit: user {notification.UserName} created");
        return Task.CompletedTask;
    }
}
```

### 3. Publish

```csharp
public class AccountService(IMediator mediator)
{
    public async Task RegisterUser(string username)
    {
        // Save to DB...
        await mediator.Publish(new UserCreated(username));
    }
}
```

You can also inject `IPublisher` if you only need notification publishing.

---

## 🧬 Pipeline Behaviors

Pipeline behaviors wrap the handler and can run logic before and after it.

### Signature (correct order)

```csharp
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
```

### Example behavior

```csharp
public sealed class TimingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var response = await next(cancellationToken);
        sw.Stop();
        Console.WriteLine($"{typeof(TRequest).Name} took {sw.ElapsedMilliseconds} ms");
        return response;
    }
}
```

### Ordering

Use the attribute (lower value runs first / outermost):

```csharp
[PipelineOrder(1)]
public sealed class FirstBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("First behavior");
        return await next(cancellationToken);
    }
}

[PipelineOrder(2)]
public sealed class SecondBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("Second behavior");
        return await next(cancellationToken);
    }
}
```

Or supply the order when registering:

```csharp
builder.Services.AddInterlink(options =>
{
    options.AddBehavior(typeof(FirstBehavior<,>), order: 1);
    options.AddBehavior(typeof(SecondBehavior<,>), order: 2);
});
```

---

## 🔄 Pre and Post Processors

Pre-processors run **before** the pipeline.  
Post-processors run **after** a successful pipeline.

```csharp
public sealed class MyRequestPreProcessor : IRequestPreProcessor<GetAllPets.Query>
{
    public Task Process(GetAllPets.Query request, CancellationToken cancellationToken)
    {
        Console.WriteLine("[Pre] GetAllPets");
        return Task.CompletedTask;
    }
}

public sealed class MyRequestPostProcessor : IRequestPostProcessor<GetAllPets.Query, List<string>>
{
    public Task Process(GetAllPets.Query request, List<string> response, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Post] returned {response.Count} pets");
        return Task.CompletedTask;
    }
}
```

They are discovered automatically by `AddInterlink()`.

---

## 📡 Streaming requests (v1.6)

```csharp
public sealed record ExportPetsStream : IStreamRequest<PetDto>;

public sealed class ExportPetsStreamHandler : IStreamRequestHandler<ExportPetsStream, PetDto>
{
    public async IAsyncEnumerable<PetDto> Handle(
        ExportPetsStream request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var pet in _repository.StreamAllAsync(cancellationToken))
            yield return pet;
    }
}

// Consumer
await foreach (var pet in sender.CreateStream(new ExportPetsStream(), ct))
{
    // process each item
}

// or via IMediator
await foreach (var pet in mediator.CreateStream(new ExportPetsStream(), ct))
{
    // ...
}
```

Register stream pipeline behaviors:

```csharp
builder.Services.AddInterlink(options =>
{
    options.AddStreamBehavior(typeof(MyStreamBehavior<,>));
}, typeof(MyHandler).Assembly);
```

## ⚡ Notification publish strategies (v1.6)

```csharp
builder.Services.AddInterlink(options =>
{
    options.PublishStrategy = PublishStrategy.Parallel; // or Sequential (default)
}, typeof(MyHandler).Assembly);
```

- **Sequential** — handlers run one after another (ordering preserved).
- **Parallel** — handlers run concurrently via `Task.WhenAll`.

## 🔔 Notification pipeline behaviors (v1.6)

```csharp
public sealed class NotificationLoggingBehavior<TNotification> : INotificationPipelineBehavior<TNotification>
    where TNotification : INotification
{
    public async Task Handle(
        TNotification notification,
        NotificationHandlerDelegate next,
        CancellationToken cancellationToken)
    {
        // before
        await next(cancellationToken);
        // after
    }
}

builder.Services.AddInterlink(options =>
{
    options.AddNotificationBehavior(typeof(NotificationLoggingBehavior<>));
});
```

## ⏱️ Request timeout (v1.6)

```csharp
builder.Services.AddInterlink(options =>
{
    options.DefaultRequestTimeout = TimeSpan.FromSeconds(30);
}, typeof(MyHandler).Assembly);
```

Registers `TimeoutBehavior<,>` automatically. Exceeding the timeout throws `TimeoutException`.

---

## 📋 Built-in Logging Behavior

```bash
dotnet add package Interlink.Extensions.Logging
```

```csharp
builder.Services.AddInterlinkLogging();
```

This registers `LoggingBehavior<TRequest, TResponse>`, which logs:

- request start  
- successful completion + elapsed milliseconds  
- exceptions  

---

## ✅ FluentValidation Integration

```bash
dotnet add package Interlink.Extensions.Validation
```

```csharp
// Registers ValidationBehavior + scans for IValidator<T>
builder.Services.AddInterlinkValidation(typeof(CreateUserValidator).Assembly);
```

Example validator:

```csharp
public sealed class CreateUserValidator : AbstractValidator<CreateUser.Command>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
```

When validation fails, a `FluentValidation.ValidationException` is thrown (mapped to 400 by the ASP.NET Core filter if you use it).

---

## 🌐 ASP.NET Core Integration

```bash
dotnet add package Interlink.AspNetCore
```

```csharp
builder.Services.AddControllers();
builder.Services.AddInterlinkAspNetCore();   // adds InterlinkExceptionFilter
```

The filter maps:

| Exception                    | HTTP Status | Response              |
|-----------------------------|-------------|-----------------------|
| `HandlerNotFoundException`  | 404         | ProblemDetails        |
| `ValidationException`*      | 400         | ValidationProblemDetails |

\* FluentValidation support is optional and detected at runtime (no hard dependency).

---

## 🔍 Analyzer (missing handler detection)

```bash
dotnet add package Interlink.Analyzers
```

| Id | Meaning |
|----|---------|
| **ILINK001** | Request has no handler |
| **ILINK002** | Multiple handlers for the same request (v1.6) |

---

## 📦 API Overview

### Core contracts

```csharp
public interface IRequest<out TResponse> { }

// Non-generic form (equivalent to IRequest<Unit>)
public interface IRequest : IRequest<Unit> { }

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface INotification { }

public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}
```

### Unit

```csharp
/// <summary>
/// Represents a void response. Use this when a request does not return a meaningful value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    public static readonly Unit Value = default;
    public static Task<Unit> Task => System.Threading.Tasks.Task.FromResult(Value);
    ...
}
```

- Prefer `IRequest` (or `IRequest<Unit>`) for commands that only perform an action.
- Always return `Unit.Value` (or `Task.FromResult(Unit.Value)`) from the corresponding handler.

### Sender, Publisher & Mediator

```csharp
public interface ISender
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
    Task Send(IRequest request, CancellationToken cancellationToken = default);   // convenience for Unit
}

public interface IPublisher
{
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}

/// <summary>
/// Unified mediator that combines request/response and notification publishing.
/// </summary>
public interface IMediator : ISender, IPublisher
{
}
```

### Pipeline

```csharp
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken = default);

public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
```

### Pre / Post processors

```csharp
public interface IRequestPreProcessor<in TRequest> where TRequest : notnull
{
    Task Process(TRequest request, CancellationToken cancellationToken);
}

public interface IRequestPostProcessor<in TRequest, in TResponse> where TRequest : notnull
{
    Task Process(TRequest request, TResponse response, CancellationToken cancellationToken);
}
```

### Streaming requests
```csharp
public interface IStreamRequest<out TResponse> { }

public interface IStreamRequestHandler<in TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
```

Register stream pipeline behaviors:
```csharp
builder.Services.AddInterlink(opt =>
{
    opt.AddStreamBehavior(typeof(StreamLoggingBehavior<,>));
}, typeof(MyHandler).Assembly);
```

### Exception

```csharp
public class HandlerNotFoundException : InvalidOperationException
{
    public Type RequestType { get; }
    public Type? HandlerType { get; }
}
```

---

## 🚀 Roadmap status

| Version   | Status     | Highlights                                                                 |
|-----------|------------|----------------------------------------------------------------------------|
| 1.0 – 1.3 | ✅ Released | Core mediator, notifications, pipelines, pre/post, performance             |
| 1.4       | ✅ Released | .NET Standard 2.0+                                                         |
| 1.5       | ✅ Released | Logging, Validation, ASP.NET Core, Analyzer, exceptions, ordering fixes    |
| 1.5.1     | ✅ Released | `Unit` support for fire-and-forget / void commands (`IRequest` / `IRequest<Unit>`) |
| 1.5.2     | ✅ Released | Added unified `IMediator` interface (composes `ISender` + `IPublisher`)    |
| **1.6**   | 🔄 Current  | Streaming requests, notification publish strategies, notification pipeline behaviors, request timeout behavior |


### Future ideas

* Request cancellation
* Metrics & tracing support
* Dynamic / externalized pipeline configuration

---

## 📜 License

MIT License © ManuHub