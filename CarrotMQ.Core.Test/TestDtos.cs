using CarrotMQ.Core.Dto;
using CarrotMQ.Core.EndPoints;

namespace CarrotMQ.Core.Test;

public sealed class TestQueue : QueueEndPoint
{
    public const string TestQueueName = "MyQueue";

    public TestQueue() : base(TestQueueName)
    {
    }
}

public sealed class TestExchange : ExchangeEndPoint
{
    public const string TestExchangeName = "MyExchange";

    public TestExchange() : base(TestExchangeName)
    {
    }
}

public class MyDto : IEvent<MyDto, TestExchange>, ICommand<MyDto, TestResponse, TestQueue>, IQuery<MyDto, TestResponse, TestQueue>
{
    public MyDto(int testValue)
    {
        TestValue = testValue;
    }

    public int TestValue { get; }
}

public class CustomRoutingKeyDto : ICustomRoutingEvent<CustomRoutingKeyDto>
{
    public const string CustomExchange = "CustomExchange";
    public const string CustomRoutingKey = "CustomRoutingKey";

    public CustomRoutingKeyDto(int testValue)
    {
        TestValue = testValue;
    }

    public int TestValue { get; }

    public string Exchange { get; set; } = CustomExchange;

    public string RoutingKey { get; set; } = CustomRoutingKey;
}

public class TestResponse;