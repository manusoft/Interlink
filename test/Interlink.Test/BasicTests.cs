namespace Interlink.Test;

public class BasicTests
{
    [Fact]
    public async Task CanSend_EchoRequest_ReturnsResponse()
    {
        var services = new ServiceCollection();
        services.AddInterlink(null, typeof(BasicTests).Assembly);

        // Resolve ISender from the service provider
        var serviceProvider = services.BuildServiceProvider();
        var sender = serviceProvider.GetRequiredService<ISender>();

        var response = await sender.Send(new EchoRequest("Hello"));

        Assert.Equal("Echo: Hello", response);
    }

    public record EchoRequest(string Message) : IRequest<string>;

    public class EchoHandler : IRequestHandler<EchoRequest, string>
    {
        public Task<string> Handle(EchoRequest request, CancellationToken cancellationToken)
            => Task.FromResult($"Echo: {request.Message}");
    }

}