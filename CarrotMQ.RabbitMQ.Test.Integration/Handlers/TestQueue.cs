using CarrotMQ.Core.EndPoints;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

public class TestQueue : QueueEndPoint
{
    public const string Name = "test.integration.queue";

    public TestQueue() : base(Name)
    {
    }
}