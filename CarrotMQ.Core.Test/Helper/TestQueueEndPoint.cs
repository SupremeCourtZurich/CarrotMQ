using CarrotMQ.Core.EndPoints;

namespace CarrotMQ.Core.Test.Helper;

public class TestQueueEndPoint : QueueEndPoint
{
    public TestQueueEndPoint() : base("TestQueue")
    {
    }
}