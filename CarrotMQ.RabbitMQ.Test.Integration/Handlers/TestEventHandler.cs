using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

public sealed class TestEventHandler : EventHandlerBase<TestEvent>
{
    private readonly ReceivedMessages _receivedMessages;

    public TestEventHandler(ReceivedMessages receivedMessages)
    {
        _receivedMessages = receivedMessages;
    }

    public override async Task<IHandlerResult> HandleAsync(TestEvent @event, ConsumerContext consumerContext, CancellationToken cancellationToken)
    {
        await _receivedMessages.WriteAsync(@event.Id, cancellationToken).ConfigureAwait(false);

        return Ok();
    }
}