using CarrotMQ.Core.Dto;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

public class ExchangeEndPointCmd : DtoBase, ICommand<ExchangeEndPointCmd, ExchangeEndPointCmd.Response, TestExchange>
{
    public ExchangeEndPointCmd(int id)
    {
        Id = id;
    }

    public TimeSpan TaskWaitDuration { get; set; }

    public TimeSpan ThreadWaitDuration { get; set; }

    public int WaitDurationCount { get; set; }

    public bool ThrowException { get; set; }

    public bool ReturnError { get; set; }

    public bool ReturnErrorWithValidationErrors { get; set; }

    public bool ReturnCustomStatusCode { get; set; }

    public bool DoRetry { get; set; }

    public bool BadRequest { get; set; }

    public bool DoReject { get; set; }

    public class Response
    {
        public int Id { get; set; }

        public static string GetRoutingKey()
        {
            return $"{nameof(ExchangeEndPointCmd)}->Response()";
        }
    }
}