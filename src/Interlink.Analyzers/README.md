![Static Badge](https://img.shields.io/badge/Interlink.Analyzers-blue)
![NuGet Version](https://img.shields.io/nuget/v/Interlink.Analyzers)
![NuGet Downloads](https://img.shields.io/nuget/dt/Interlink.Analyzers)

# Interlink.Analyzers

Roslyn analyzer for the [Interlink](https://www.nuget.org/packages/Interlink) mediator library.

## Installation

```bash
dotnet add package Interlink.Analyzers
```

This is a development dependency (analyzer only). It does not add any runtime assemblies.

## What it detects

**ILINK001** – Missing request handler

**ILINK002** – Duplicate request handler (more than one handler for the same request type)  

Raised when a type implements `IRequest<TResponse>` but no corresponding  
`IRequestHandler<TRequest, TResponse>` is found in the compilation.

### Example of ILINK001 - No Handler

```csharp
// Warning ILINK001: No handler found for request type 'GetAllPets.Query'
public sealed record GetAllPetsQuery : IRequest<List<string>>;
```

Add a handler to clear the diagnostic:

```csharp
public sealed class GetAllPetsHandler : IRequestHandler<GetAllPetsQuery, List<string>>
{
    public Task<List<string>> Handle(GetAllPetsQuery request, CancellationToken cancellationToken)
        => Task.FromResult(new List<string>());
}
```

### Example of ILINK002 - Duplicate Handler
```csharp
// Warning ILINK002: Multiple handlers found for request type 'GetSomthing'
public record GetSomthing : IRequest;

public class Handler1 : IRequestHandler<GetSomthing>
{
    public Task<Unit> Handle(GetSomthing request, CancellationToken cancellationToken)
        => Unit.Task;
}

public class Handler2 : IRequestHandler<GetSomthing> // duplicate
{
    public Task<Unit> Handle(GetSomthing request, CancellationToken cancellationToken)
        => Unit.Task;
}
```

Remove one of the handlers to clear the diagnostic.

## License

MIT © ManuHub