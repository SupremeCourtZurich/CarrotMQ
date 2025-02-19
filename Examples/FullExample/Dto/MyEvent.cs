using CarrotMQ.Core.Dto;

namespace Dto;

public class MyEvent : IEvent<MyEvent, MyExchange>
{
    public string Message { get; set; }
}