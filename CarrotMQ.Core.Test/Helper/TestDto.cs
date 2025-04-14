using CarrotMQ.Core.Dto;

namespace CarrotMQ.Core.Test.Helper;

public class TestDto : IEvent<TestDto, TestExchangeEndPoint>, ICommand<TestDto, TestResponse, TestQueueEndPoint>,
    IQuery<TestDto, TestResponse, TestQueueEndPoint>
{
    public TestDto(int testValue)
    {
        TestValue = testValue;
    }

    public int TestValue { get; }
}