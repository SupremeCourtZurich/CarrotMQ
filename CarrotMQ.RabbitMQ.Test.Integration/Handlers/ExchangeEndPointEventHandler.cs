using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class ExchangeEndPointEventHandler : EventHandlerBase<ExchangeEndPointEvent>
{
    private readonly BarrierBag _barrierBag;
    private readonly ReceivedMessages _receivedMessages;

    public ExchangeEndPointEventHandler(ReceivedMessages receivedMessages, BarrierBag barrierBag)
    {
        _receivedMessages = receivedMessages;
        _barrierBag = barrierBag;
    }

    public override async Task<IHandlerResult> HandleAsync(
        ExchangeEndPointEvent @event,
        ConsumerContext consumerContext,
        CancellationToken cancellationToken)
    {
        if (@event.BarrierId is not null)
        {
            if (_barrierBag.Barriers.TryGetValue(@event.BarrierId.Value, out Barrier? value))
            {
                Console.WriteLine($"Received:{@event.Id} and waiting");

                value.SignalAndWait(cancellationToken);
            }
        }

        await _receivedMessages.WriteAsync(@event.Id, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Received:{@event.Id}");

        if (@event.TaskWaitDuration > TimeSpan.Zero)
        {
            try
            {
                for (var i = 0; i < @event.WaitDurationCount; i++)
                {
                    Console.WriteLine($"Task delay {i} {@event.TaskWaitDuration.TotalMilliseconds}ms");
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