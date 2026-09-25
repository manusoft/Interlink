using Interlink.Contracts;

namespace Interlink;

internal sealed class Mediator : IMediator
{
    private readonly ISender _sender;
    private readonly IPublisher _publisher;

    public Mediator(ISender sender, IPublisher publisher)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        => _sender.Send(request, cancellationToken);

    public Task Send(IRequest request, CancellationToken cancellationToken = default)
        => _sender.Send(request, cancellationToken);

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default)
        => _sender.CreateStream(request, cancellationToken);

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
        => _publisher.Publish(notification, cancellationToken);
}
