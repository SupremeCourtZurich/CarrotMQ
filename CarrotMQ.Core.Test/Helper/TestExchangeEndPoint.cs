using CarrotMQ.Core.EndPoints;

namespace CarrotMQ.Core.Test.Helper;

public class TestExchangeEndPoint : ExchangeEndPoint
{
    public TestExchangeEndPoint() : base("TestExchange")
    {
    }
}