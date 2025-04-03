using CarrotMQ.Core.Dto;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Test.Helper;
using RetryResult = CarrotMQ.Core.Handlers.HandlerResults.RetryResult;

namespace CarrotMQ.Core.Test.MessageProcessing;

[TestClass]
public class ResponseHandlerBaseTest
{
    [TestMethod]
    public void HandlerResponse_Ok_Check_OkResult()
    {
        TestResponseHandler handler = new();

        var result = handler.Ok();

        Assert.IsNull(result.Response.Content);
        Assert.IsNull(result.Response.Error);
        Assert.IsInstanceOfType(result, typeof(OkResult));
        Assert.AreEqual(DeliveryStatus.Ack, result.DeliveryStatus);
        Assert.AreEqual(CarrotStatusCode.Ok, result.Response.StatusCode);
    }

    [TestMethod]
    public void HandlerResponse_Reject_Check_RejectResult()
    {
        TestResponseHandler handler = new();

        var result = handler.Reject();

        Assert.IsNull(result.Response.Content);
        Assert.IsNull(result.Response.Error);
        Assert.IsInstanceOfType(result, typeof(RejectResult));
        Assert.AreEqual(DeliveryStatus.Reject, result.DeliveryStatus);
        Assert.AreEqual(CarrotStatusCode.InternalServerError, result.Response.StatusCode);
    }

    [TestMethod]
    public void HandlerResponse_Retry_Check_RetryResult()
    {
        TestResponseHandler handler = new();

        var result = handler.Retry();

        Assert.IsNull(result.Response.Content);
        Assert.IsNull(result.Response.Error);
        Assert.IsInstanceOfType(result, typeof(RetryResult));
        Assert.AreEqual(DeliveryStatus.Retry, result.DeliveryStatus);
        Assert.AreEqual(CarrotStatusCode.InternalServerError, result.Response.StatusCode);
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private class Response;

    // ReSharper disable once ClassNeverInstantiated.Local
    private class TestRequest : IQuery<TestRequest, Response, TestQueueEndPoint>;

    private class TestResponseHandler : ResponseHandlerBase<TestRequest, Response>
    {
        public override Task<IHandlerResult> HandleAsync(
            CarrotResponse<TestRequest, Response> carrotResponse,
            ConsumerContext consumerContext,
            CancellationToken cancellationToken)
        {
            return new OkResult().AsTask();
        }
    }
}