using CarrotMQ.Core.Dto;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

public class ExchangeEndPointCustomRoutingEvent : DtoBase, ICustomRoutingEvent<ExchangeEndPointCustomRoutingEvent>
{
    public ExchangeEndPointCustomRoutingEvent(string exchange, string routingKey, int id)
    {
        Id = id;
        Exchange = exchange;
        RoutingKey = routingKey;
    }

    public TimeSpan TaskWaitDuration { get; set; }

    public TimeSpan ThreadWaitDuration { get; set; }

    public int WaitDurationCount { get; set; }

    public bool ThrowException { get; set; }

    public bool ReturnError { get; set; }

    public bool DoRetry { get; set; }

    public bool BadRequest { get; set; }

    public bool DoReject { get; set; }

    public string Exchange { get; set; }

    public string RoutingKey { get; set; }

    public static string GetRoutingKey()
    {
        return "ExchangeEndPointCustomRoutingEvent_RoutingKey";
    }
}