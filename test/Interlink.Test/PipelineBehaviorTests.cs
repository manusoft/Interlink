namespace Interlink.Test;

public class PipelineBehaviorTests
{
    [Fact]
    public async Task Executes_PipelineBehaviors_InOrder()
    {
        var services = new ServiceCollection();
        services.AddInterlink(opt => opt.AddBehavior(typeof(LoggingBehavior<,>)), typeof(PipelineBehaviorTests).Assembly);

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        LoggingBehavior<Ping, string>.Logs.Clear();
        var response = await mediator.Send(new Ping());

        Assert.Equal("Pong", response);
        Assert.Contains("[Before] Ping", LoggingBehavior<Ping, string>.Logs);
        Assert.Contains("[After] Ping", LoggingBehavior<Ping, string>.Logs);
    }
}

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public static List<string> Logs = new();

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        Logs.Add($"[Before] {typeof(TRequest).Name}");
        var response = await next(cancellationToken);
        Logs.Add($"[After] {typeof(TRequest).Name}");
        return response;
    }
}

public class Ping : IRequest<string> { }

public class PingHandler : IRequestHandler<Ping, string>
{
    public Task<string> Handle(Ping request, CancellationToken cancellationToken) => Task.FromResult("Pong");
}
