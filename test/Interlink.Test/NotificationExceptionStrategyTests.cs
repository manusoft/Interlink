namespace Interlink.Test;

/// <summary>
/// Tests for <see cref="NotificationExceptionStrategy"/> and
/// <see cref="INotificationExceptionHandler{TNotification,TException}"/>.
/// Each scenario uses its own notification type so assembly scanning does not
/// cross-register handlers across tests.
/// </summary>
public class NotificationExceptionStrategyTests
{
    [Fact]
    public async Task StopOnFirst_Throws_NotificationHandlerException()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new StopOnFirstProbe());
        services.AddInterlink(options =>
        {
            options.PublishStrategy = PublishStrategy.Sequential;
            options.NotificationExceptionStrategy = NotificationExceptionStrategy.StopOnFirst;
        }, typeof(NotificationExceptionStrategyTests).Assembly);

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        var ex = await Assert.ThrowsAsync<NotificationHandlerException>(
            () => publisher.Publish(new StopOnFirstNotification()));

        Assert.Equal(typeof(StopOnFirstNotification), ex.NotificationType);
        Assert.Equal(typeof(StopOnFirst_ThrowingHandler), ex.HandlerType);
        Assert.Contains("stop-boom", ex.InnerException!.Message);
        Assert.IsNotType<AggregateException>(ex);
    }

    [Fact]
    public async Task Aggregate_RunsAllHandlers_ThenThrowsAggregateException()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new AggregateProbe());
        services.AddInterlink(options =>
        {
            options.PublishStrategy = PublishStrategy.Sequential;
            options.NotificationExceptionStrategy = NotificationExceptionStrategy.Aggregate;
        }, typeof(NotificationExceptionStrategyTests).Assembly);

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();
        var probe = provider.GetRequiredService<AggregateProbe>();

        var ex = await Assert.ThrowsAsync<AggregateException>(
            () => publisher.Publish(new AggregateNotification()));

        Assert.Equal(2, ex.InnerExceptions.Count);
        Assert.All(ex.InnerExceptions, e => Assert.IsType<NotificationHandlerException>(e));
        Assert.Equal(1, probe.SuccessCount);
        Assert.Equal(2, probe.ThrowCount);
    }

    [Fact]
    public async Task Continue_RunsAllHandlers_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ContinueProbe());
        services.AddInterlink(options =>
        {
            options.PublishStrategy = PublishStrategy.Sequential;
            options.NotificationExceptionStrategy = NotificationExceptionStrategy.Continue;
        }, typeof(NotificationExceptionStrategyTests).Assembly);

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();
        var probe = provider.GetRequiredService<ContinueProbe>();

        await publisher.Publish(new ContinueNotification());

        Assert.Equal(1, probe.ThrowCount);
        Assert.Equal(1, probe.SuccessCount);
    }

    [Fact]
    public async Task ExceptionHandler_IsInvoked_OnHandlerFailure()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ExceptionHandlerProbe());
        services.AddInterlink(options =>
        {
            options.PublishStrategy = PublishStrategy.Sequential;
            options.NotificationExceptionStrategy = NotificationExceptionStrategy.Continue;
        }, typeof(NotificationExceptionStrategyTests).Assembly);

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();
        var exceptionProbe = provider.GetRequiredService<ExceptionHandlerProbe>();

        await publisher.Publish(new ExceptionHandlerNotification());

        Assert.Equal(1, exceptionProbe.CallCount);
        Assert.NotNull(exceptionProbe.LastException);
        Assert.Contains("eh-boom", exceptionProbe.LastException!.Message);
    }

    [Fact]
    public async Task Aggregate_Parallel_CollectsFailuresFromAllHandlers()
    {
        var services = new ServiceCollection();
        services.AddInterlink(options =>
        {
            options.PublishStrategy = PublishStrategy.Parallel;
            options.NotificationExceptionStrategy = NotificationExceptionStrategy.Aggregate;
        }, typeof(NotificationExceptionStrategyTests).Assembly);

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();

        var ex = await Assert.ThrowsAsync<AggregateException>(
            () => publisher.Publish(new ParallelAggregateNotification()));

        Assert.Equal(2, ex.InnerExceptions.Count);
        Assert.All(ex.InnerExceptions, e => Assert.IsType<NotificationHandlerException>(e));
    }

    [Fact]
    public async Task ExceptionHandler_Runs_Before_StopOnFirst_Rethrows()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ExceptionHandlerProbe());
        services.AddInterlink(options =>
        {
            options.PublishStrategy = PublishStrategy.Sequential;
            options.NotificationExceptionStrategy = NotificationExceptionStrategy.StopOnFirst;
        }, typeof(NotificationExceptionStrategyTests).Assembly);

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IPublisher>();
        var exceptionProbe = provider.GetRequiredService<ExceptionHandlerProbe>();

        await Assert.ThrowsAsync<NotificationHandlerException>(
            () => publisher.Publish(new ExceptionHandlerNotification()));

        Assert.Equal(1, exceptionProbe.CallCount);
    }
}

// ─── StopOnFirst ─────────────────────────────────────────────

public sealed class StopOnFirstProbe
{
    private int _throwCount;
    private int _secondRan;

    public int ThrowCount => _throwCount;
    public int SecondRanCount => _secondRan;

    public void MarkThrow() => Interlocked.Increment(ref _throwCount);
    public void MarkSecond() => Interlocked.Increment(ref _secondRan);
}

public sealed class StopOnFirstNotification : INotification;

public sealed class StopOnFirst_ThrowingHandler : INotificationHandler<StopOnFirstNotification>
{
    private readonly StopOnFirstProbe _probe;
    public StopOnFirst_ThrowingHandler(StopOnFirstProbe probe) => _probe = probe;

    public Task Handle(StopOnFirstNotification notification, CancellationToken cancellationToken)
    {
        _probe.MarkThrow();
        throw new InvalidOperationException("stop-boom");
    }
}

public sealed class StopOnFirst_SecondHandler : INotificationHandler<StopOnFirstNotification>
{
    private readonly StopOnFirstProbe _probe;
    public StopOnFirst_SecondHandler(StopOnFirstProbe probe) => _probe = probe;

    public Task Handle(StopOnFirstNotification notification, CancellationToken cancellationToken)
    {
        _probe.MarkSecond();
        return Task.CompletedTask;
    }
}

// ─── Aggregate ───────────────────────────────────────────────

public sealed class AggregateProbe
{
    private int _throwCount;
    private int _successCount;

    public int ThrowCount => _throwCount;
    public int SuccessCount => _successCount;

    public void MarkThrow() => Interlocked.Increment(ref _throwCount);
    public void MarkSuccess() => Interlocked.Increment(ref _successCount);
}

public sealed class AggregateNotification : INotification;

public sealed class Aggregate_FirstThrows : INotificationHandler<AggregateNotification>
{
    private readonly AggregateProbe _probe;
    public Aggregate_FirstThrows(AggregateProbe probe) => _probe = probe;

    public Task Handle(AggregateNotification notification, CancellationToken cancellationToken)
    {
        _probe.MarkThrow();
        throw new InvalidOperationException("agg-1");
    }
}

public sealed class Aggregate_SecondSucceeds : INotificationHandler<AggregateNotification>
{
    private readonly AggregateProbe _probe;
    public Aggregate_SecondSucceeds(AggregateProbe probe) => _probe = probe;

    public Task Handle(AggregateNotification notification, CancellationToken cancellationToken)
    {
        _probe.MarkSuccess();
        return Task.CompletedTask;
    }
}

public sealed class Aggregate_ThirdThrows : INotificationHandler<AggregateNotification>
{
    private readonly AggregateProbe _probe;
    public Aggregate_ThirdThrows(AggregateProbe probe) => _probe = probe;

    public Task Handle(AggregateNotification notification, CancellationToken cancellationToken)
    {
        _probe.MarkThrow();
        throw new InvalidOperationException("agg-3");
    }
}

// ─── Continue ────────────────────────────────────────────────

public sealed class ContinueProbe
{
    private int _throwCount;
    private int _successCount;

    public int ThrowCount => _throwCount;
    public int SuccessCount => _successCount;

    public void MarkThrow() => Interlocked.Increment(ref _throwCount);
    public void MarkSuccess() => Interlocked.Increment(ref _successCount);
}

public sealed class ContinueNotification : INotification;

public sealed class Continue_ThrowingHandler : INotificationHandler<ContinueNotification>
{
    private readonly ContinueProbe _probe;
    public Continue_ThrowingHandler(ContinueProbe probe) => _probe = probe;

    public Task Handle(ContinueNotification notification, CancellationToken cancellationToken)
    {
        _probe.MarkThrow();
        throw new InvalidOperationException("continue-boom");
    }
}

public sealed class Continue_SuccessHandler : INotificationHandler<ContinueNotification>
{
    private readonly ContinueProbe _probe;
    public Continue_SuccessHandler(ContinueProbe probe) => _probe = probe;

    public Task Handle(ContinueNotification notification, CancellationToken cancellationToken)
    {
        _probe.MarkSuccess();
        return Task.CompletedTask;
    }
}

// ─── Exception handler ───────────────────────────────────────

public sealed class ExceptionHandlerProbe
{
    private int _callCount;

    public int CallCount => _callCount;
    public Exception? LastException { get; private set; }

    public void Record(Exception ex)
    {
        Interlocked.Increment(ref _callCount);
        LastException = ex;
    }
}

public sealed class ExceptionHandlerNotification : INotification;

public sealed class ExceptionHandler_ThrowingHandler : INotificationHandler<ExceptionHandlerNotification>
{
    public Task Handle(ExceptionHandlerNotification notification, CancellationToken cancellationToken)
        => throw new InvalidOperationException("eh-boom");
}

public sealed class ExceptionHandler_Capture : INotificationExceptionHandler<ExceptionHandlerNotification, Exception>
{
    private readonly ExceptionHandlerProbe _probe;
    public ExceptionHandler_Capture(ExceptionHandlerProbe probe) => _probe = probe;

    public Task Handle(
        ExceptionHandlerNotification notification,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _probe.Record(exception);
        return Task.CompletedTask;
    }
}

// ─── Parallel Aggregate ──────────────────────────────────────

public sealed class ParallelAggregateNotification : INotification;

public sealed class Parallel_FirstThrows : INotificationHandler<ParallelAggregateNotification>
{
    public Task Handle(ParallelAggregateNotification notification, CancellationToken cancellationToken)
        => throw new InvalidOperationException("par-1");
}

public sealed class Parallel_SecondThrows : INotificationHandler<ParallelAggregateNotification>
{
    public Task Handle(ParallelAggregateNotification notification, CancellationToken cancellationToken)
        => throw new InvalidOperationException("par-2");
}
