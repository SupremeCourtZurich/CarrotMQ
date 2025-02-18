using CarrotMQ.Core.Dto;

namespace Dto;

#region MyEventDefinition
public class MyEvent : IEvent<MyEvent, MyExchange>
{
    public string Message { get; set; }
}
#endregion