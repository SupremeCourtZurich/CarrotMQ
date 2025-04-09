using CarrotMQ.Core.EndPoints;

namespace CarrotMQ.Core.Test.Helper;

public class TestQueueEndPoint : QueueEndPoint
{
    public const string TestQueueName = "TestQueue";

    public TestQueueEndPoint() : base(TestQueueName)
    {
    }
}