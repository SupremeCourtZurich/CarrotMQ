using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Handlers.HandlerResults;

namespace CarrotMQ.Core.Handlers;

internal class SubscriptionResponseHandler<TRequest, TResponse> : ResponseHandlerBase<TRequest, TResponse>
    where TRequest : _IRequest<TRequest, TResponse> where TResponse : class
{
    private readonly ResponseSubscription<TRequest, TResponse> _eventSubscription;

    public SubscriptionResponseHandler(ResponseSubscription<TRequest, TResponse> eventSubscription)
    {
        _eventSubscription = eventSubscription;
    }

    /// <inheritdoc />
    public override async Task<IHandlerResult> HandleAsync(
        CarrotResponse<TRequest, TResponse> message,
        ConsumerContext consumerContext,
        CancellationToken cancellationToken)
    {
        await _eventSubscription.OnResponseReceived(message, consumerContext).ConfigureAwait(false);

        return Ok();
    }
}