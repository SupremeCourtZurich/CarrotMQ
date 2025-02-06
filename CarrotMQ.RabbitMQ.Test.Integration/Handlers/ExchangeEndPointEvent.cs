using CarrotMQ.Core.Dto;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

public class ExchangeEndPointEvent : DtoBase, IEvent<ExchangeEndPointEvent, TestExchange>
{
    public ExchangeEndPointEvent(int id)
    {
        Id = id;
    }

    public TimeSpan TaskWaitDuration { get; set; }

    public TimeSpan ThreadWaitDuration { get; set; }

    public int WaitDurationCount { get; set; }

    public bool ThrowException { get; set; }

    public bool ReturnError { get; set; }

    public bool DoRetry { get; set; }

    public bool BadRequest { get; set; }

    public bool DoReject { get; set; }
}