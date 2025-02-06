using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Handlers.HandlerResults;

namespace CarrotMQ.Core.Handlers;

internal class SubscriptionEventHandler<TEvent> : EventHandlerBase<TEvent> where TEvent : _IMessage<TEvent, NoResponse>
{
    private readonly EventSubscription<TEvent> _eventSubscription;

    public SubscriptionEventHandler(EventSubscription<TEvent> eventSubscription)
    {
        _eventSubscription = eventSubscription;
    }

    /// <inheritdoc />
    public override async Task<IHandlerResult> HandleAsync(TEvent message, ConsumerContext consumerContext, CancellationToken cancellationToken)
    {
        await _eventSubscription.OnEventReceived(message, consumerContext).ConfigureAwait(false);

        return Ok();
    }
}