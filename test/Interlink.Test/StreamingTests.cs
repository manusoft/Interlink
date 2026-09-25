using System.Runtime.CompilerServices;

namespace Interlink.Test;

public class StreamingTests
{
    [Fact]
    public async Task CreateStream_YieldsAllItems_InOrder()
    {
        var services = new ServiceCollection();
        services.AddInterlink(null, typeof(StreamingTests).Assembly);

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var items = new List<int>();
        await foreach (var n in sender.CreateStream(new NumberStreamRequest(1, 5)))
            items.Add(n);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, items);
    }

    [Fact]
    public async Task CreateStream_ViaMediator_Works()
    {
        var services = new ServiceCollection();
        services.AddInterlink(null, typeof(StreamingTests).Assembly);

        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var items = new List<string>();
        await foreach (var s in mediator.CreateStream(new StringStreamRequest()))
            items.Add(s);

        Assert.Equal(new[] { "a", "b", "c" }, items);
    }

    [Fact]
    public async Task CreateStream_NoHandler_ThrowsHandlerNotFoundException()
    {
        var services = new ServiceCollection();
        services.AddInterlink(null, typeof(StreamingTests).Assembly);

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        await Assert.ThrowsAsync<HandlerNotFoundException>(async () =>
        {
            await foreach (var _ in sender.CreateStream(new OrphanStreamRequest()))
            {
            }
        });
    }

    [Fact]
    public async Task CreateStream_HonorsCancellation()
    {
        var services = new ServiceCollection();
        services.AddInterlink(null, typeof(StreamingTests).Assembly);

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in sender.CreateStream(new SlowStreamRequest(), cts.Token))
            {
            }
        });
    }

    [Fact]
    public async Task CreateStream_WithStreamBehavior_InvokesBehavior()
    {
        var services = new ServiceCollection();
        services.AddSingleton<StreamBehaviorProbe>();
        services.AddInterlink(options =>
        {
            // Open generic behaviors must be registered explicitly (not via type scan)
            options.AddStreamBehavior(typeof(CountingStreamBehavior<,>));
        }, typeof(StreamingTests).Assembly);

        await using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();
        var probe = provider.GetRequiredService<StreamBehaviorProbe>();

        var count = 0;
        await foreach (var _ in sender.CreateStream(new NumberStreamRequest(1, 3)))
            count++;

        Assert.Equal(3, count);
        Assert.True(probe.Entered);
        Assert.True(probe.Exited);
    }
}

// --- top-level test types (open generics must not be nested for DI) ---

public sealed record NumberStreamRequest(int From, int To) : IStreamRequest<int>;

public sealed class NumberStreamHandler : IStreamRequestHandler<NumberStreamRequest, int>
{
    public async IAsyncEnumerable<int> Handle(
        NumberStreamRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = request.From; i <= request.To; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
            await Task.Yield();
        }
    }
}

public sealed record StringStreamRequest : IStreamRequest<string>;

public sealed class StringStreamHandler : IStreamRequestHandler<StringStreamRequest, string>
{
    public async IAsyncEnumerable<string> Handle(
        StringStreamRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var s in new[] { "a", "b", "c" })
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return s;
            await Task.Yield();
        }
    }
}

public sealed record OrphanStreamRequest : IStreamRequest<int>;

public sealed record SlowStreamRequest : IStreamRequest<int>;

public sealed class SlowStreamHandler : IStreamRequestHandler<SlowStreamRequest, int>
{
    public async IAsyncEnumerable<int> Handle(
        SlowStreamRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        yield break;
    }
}

public sealed class StreamBehaviorProbe
{
    public bool Entered { get; set; }
    public bool Exited { get; set; }
}

public sealed class CountingStreamBehavior<TRequest, TResponse>
    : IStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    private readonly StreamBehaviorProbe _probe;

    public CountingStreamBehavior(StreamBehaviorProbe probe) => _probe = probe;

    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _probe.Entered = true;
        await foreach (var item in next().WithCancellation(cancellationToken))
            yield return item;
        _probe.Exited = true;
    }
}