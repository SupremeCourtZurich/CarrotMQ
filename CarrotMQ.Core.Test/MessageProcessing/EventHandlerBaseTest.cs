using CarrotMQ.Core.Dto;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.Test.Helper;
using RetryResult = CarrotMQ.Core.Handlers.HandlerResults.RetryResult;

namespace CarrotMQ.Core.Test.MessageProcessing;

[TestClass]
public class EventHandlerBaseTest
{
    [TestMethod]
    public void HandlerResponse_Ok_Check_OkResult()
    {
        TestEventHandler handler = new();

        var result = handler.Ok();

        Assert.IsNull(result.Response.Content);
        Assert.IsNull(result.Response.Error);
        Assert.IsInstanceOfType(result, typeof(OkResult));
        Assert.AreEqual(DeliveryStatus.Ack, result.DeliveryStatus);
    }

    [TestMethod]
    public void HandlerResponse_Reject_Check_RejectResult()
    {
        TestEventHandler handler = new();

        var result = handler.Reject();

        Assert.IsNull(result.Response.Content);
        Assert.IsNull(result.Response.Error);
        Assert.IsInstanceOfType(result, typeof(RejectResult));
        Assert.AreEqual(DeliveryStatus.Reject, result.DeliveryStatus);
    }

    [TestMethod]
    public void HandlerResponse_Retry_Check_RetryResult()
    {
        TestEventHandler handler = new();

        var result = handler.Retry();

        Assert.IsNull(result.Response.Content);
        Assert.IsNull(result.Response.Error);
        Assert.IsInstanceOfType(result, typeof(RetryResult));
        Assert.AreEqual(DeliveryStatus.Retry, result.DeliveryStatus);
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private class TestEvent : IEvent<TestEvent, TestExchangeEndPoint>;

    private class TestEventHandler : EventHandlerBase<TestEvent>
    {
        public override Task<IHandlerResult> HandleAsync(TestEvent @event, ConsumerContext consumerContext, CancellationToken cancellationToken)
        {
            return new OkResult().AsTask();
        }
    }
}