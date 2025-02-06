using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

public sealed class TestQueryHandler : QueryHandlerBase<TestQuery, TestQuery.Response>
{
    public override Task<IHandlerResult> HandleAsync(TestQuery @event, ConsumerContext consumerContext, CancellationToken cancellationToken)
    {
        var response = new TestQuery.Response { Id = @event.Id };

        Console.WriteLine($"Sending response {@event.Id}");

        return Task.FromResult(Ok(response));
    }
}