using CarrotMQ.Core.Dto;

namespace CarrotMQ.Core.Test.Helper;

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