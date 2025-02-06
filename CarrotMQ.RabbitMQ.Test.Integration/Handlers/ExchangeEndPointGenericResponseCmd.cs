using CarrotMQ.Core.Dto;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

public class ExchangeEndPointGenericResponseCmd : DtoBase,
    ICommand<ExchangeEndPointGenericResponseCmd, ExchangeEndPointGenericResponseCmd.GenericResponse<ExchangeEndPointGenericResponseCmd.Response>,
        TestExchange>
{
    public ExchangeEndPointGenericResponseCmd(int id)
    {
        Id = id;
    }

    public class Response
    {
        public int Id { get; set; }
    }

    public class GenericResponse<T>
    {
        public T? InnerResponse { get; set; }
    }
}