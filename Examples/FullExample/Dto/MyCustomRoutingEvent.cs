using CarrotMQ.Core.Dto;

namespace Dto;

public class MyCustomRoutingEvent : ICustomRoutingEvent<MyCustomRoutingEvent>
{
    public required string Exchange { get; set; }

    public required string RoutingKey { get; set; }

    public string Message { get; set; } = string.Empty;
}