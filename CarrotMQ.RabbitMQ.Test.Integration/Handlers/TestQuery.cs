using CarrotMQ.Core.Dto;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

public class TestQuery : IQuery<TestQuery, TestQuery.Response, TestExchange>
{
    public int Id { get; set; }

    public class Response
    {
        public int Id { get; set; }
    }
}