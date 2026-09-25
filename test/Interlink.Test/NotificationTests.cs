namespace Interlink.Test;

public class NotificationTests
{
    [Fact]
    public async Task Publishes_Notification_ToAllHandlers()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddSingleton<NotificationCounter>();
        services.AddScoped<INotificationHandler<MyTestNotification>, Handler1>();
        services.AddScoped<INotificationHandler<MyTestNotification>, Handler2>();

        // Register Interlink
        services.AddInterlink(assemblies: new[] { typeof(MyTestNotification).Assembly });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var counter = provider.GetRequiredService<NotificationCounter>();
        counter.Reset();

        // Act
        await mediator.Publish(new MyTestNotification());

        // Assert
        Assert.True(counter.Count >= 2); // expecting 2 handlers were invoked
    }
}

public class MyTestNotification : INotification
{
    public string Message { get; set; } = "Test notification";
}

public class NotificationCounter
{
    private int _count = 0;

    public int Count => _count;

    public void Increment() => Interlocked.Increment(ref _count);

    public void Reset() => Interlocked.Exchange(ref _count, 0);
}

public class Handler1 : INotificationHandler<MyTestNotification>
{
    private readonly NotificationCounter _counter;
    public Handler1(NotificationCounter counter) => _counter = counter;

    public Task Handle(MyTestNotification notification, CancellationToken cancellationToken)
    {
        _counter.Increment();
        return Task.CompletedTask;
    }
}

public class Handler2 : INotificationHandler<MyTestNotification>
{
    private readonly NotificationCounter _counter;
    public Handler2(NotificationCounter counter) => _counter = counter;

    public Task Handle(MyTestNotification notification, CancellationToken cancellationToken)
    {
        _counter.Increment();
        return Task.CompletedTask;
    }
}