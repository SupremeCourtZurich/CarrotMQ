using CarrotMQ.Core.EndPoints;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

public class TestExchange : ExchangeEndPoint
{
    public const string Name = "test.integration.exchange";

    public TestExchange() : base(Name)
    {
    }
}