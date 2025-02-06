using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class ExchangeEndPointCustomRoutingEventHandler : EventHandlerBase<ExchangeEndPointCustomRoutingEvent>
{
    private readonly ReceivedMessages _receivedMessages;

    public ExchangeEndPointCustomRoutingEventHandler(ReceivedMessages receivedMessages)
    {
        _receivedMessages = receivedMessages;
    }

    public override async Task<IHandlerResult> HandleAsync(
        ExchangeEndPointCustomRoutingEvent @event,
        ConsumerContext consumerContext,
        CancellationToken cancellationToken)
    {
        await _receivedMessages.WriteAsync(@event.Id, cancellationToken).ConfigureAwait(false);

        if (@event.TaskWaitDuration > TimeSpan.Zero)
        {
            try
            {
                for (var i = 0; i < @event.WaitDurationCount; i++)
                {
                    Console.WriteLine($"Task delay {i} {@event.TaskWaitDuration.Milliseconds}ms");
                    await Task.Delay(@event.TaskWaitDuration, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                return Reject();
            }
        }

        if (@event.ThreadWaitDuration > TimeSpan.Zero)
        {
            for (var i = 0; i < @event.WaitDurationCount; i++)
            {
                Console.WriteLine($"Thread sleep {i} {@event.ThreadWaitDuration.Milliseconds}ms");
                Thread.Sleep(@event.ThreadWaitDuration);
            }
        }

        if (@event.DoRetry)
        {
            return Retry();
        }

        if (@event.DoReject)
        {
            return Reject();
        }

        if (@event.ThrowException)
        {
            throw new ArgumentException($"Exception for @event {@event.Id}", nameof(@event));
        }

        return Ok();
    }
}