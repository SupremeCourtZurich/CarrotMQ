using CarrotMQ.Core.EndPoints;

namespace Dto;

public class MyQueue : QueueEndPoint
{
    public MyQueue() : base("MyQueue")
    {
    }
}