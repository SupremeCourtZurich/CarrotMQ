using CarrotMQ.Core.EndPoints;

namespace Dto;

#region MyQueueDefinition
public class MyQueue : QueueEndPoint
{
    public MyQueue() : base("my-queue")
    {
    }
}
#endregion