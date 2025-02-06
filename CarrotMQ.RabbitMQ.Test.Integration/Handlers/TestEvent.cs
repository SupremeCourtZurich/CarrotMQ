using CarrotMQ.Core.Dto;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

public class TestEvent : IEvent<TestEvent, TestExchange>
{
    public int Id { get; set; }
}