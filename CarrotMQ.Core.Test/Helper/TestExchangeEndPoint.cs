using CarrotMQ.Core.EndPoints;

namespace CarrotMQ.Core.Test.Helper;

public class TestExchangeEndPoint : ExchangeEndPoint
{
    public const string TestExchangeName = "TestExchange";

    public TestExchangeEndPoint() : base(TestExchangeName)
    {
    }
}