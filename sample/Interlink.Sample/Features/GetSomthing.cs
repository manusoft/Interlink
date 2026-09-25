using Interlink.Contracts;

namespace Interlink.Sample.Features;

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