using CarrotMQ.Core.Dto;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Test.Helper;
using RetryResult = CarrotMQ.Core.Handlers.HandlerResults.RetryResult;

namespace CarrotMQ.Core.Test.MessageProcessing;

[TestClass]
public class RequestHandlerBaseTest
{
    [TestMethod]
    public void HandlerResponse_Ok_Check_OkResult_and_StatusCode()
    {
        TestRequestHandler handler = new();
        Response response = new();

        var result = handler.Ok(response);

        Assert.AreEqual(result.Response.Content, response);
        Assert.IsNull(result.Response.Error);
        Assert.IsInstanceOfType(result, typeof(OkResult));
        Assert.AreEqual(DeliveryStatus.Ack, result.DeliveryStatus);
        Assert.AreEqual(CarrotStatusCode.Ok, result.Response.StatusCode);
    }

    [TestMethod]
    public void HandlerResponse_BadRequest_WithResponse_Check_ErrorResult_and_StatusCode()
    {
        var validationErrors = new Dictionary<string, string[]> { { "Field1", ["ErrorField1_1", "ErrorField1_2"] } };

        TestRequestHandler handler = new();
        Response response = new();

        var result = handler.BadRequest(response, "MyErrorMessage", validationErrors);

        Assert.AreEqual(result.Response.Content, response);
        Assert.AreEqual("MyErrorMessage", result.Response.Error?.Message);
        Assert.IsNotNull(result.Response.Error);
        Assert.IsTrue(result.Response.Error.Errors.ContainsKey("Field1"));
        Assert.AreEqual("ErrorField1_1", result.Response.Error.Errors["Field1"][0]);
        Assert.AreEqual("ErrorField1_2", result.Response.Error.Errors["Field1"][1]);
        Assert.IsInstanceOfType(result, typeof(ErrorResult));
        Assert.AreEqual(DeliveryStatus.Ack, result.DeliveryStatus);
        Assert.AreEqual(CarrotStatusCode.BadRequest, result.Response.StatusCode);
    }

    [TestMethod]
    public void HandlerResponse_BadRequest_WithoutResponse_Check_ErrorResult_and_StatusCode()
    {
        var validationErrors = new Dictionary<string, string[]> { { "Field1", ["ErrorField1_1", "ErrorField1_2"] } };

        TestRequestHandler handler = new();

        var result = handler.BadRequest("MyErrorMessage", validationErrors);

        Assert.IsNull(result.Response.Content);
        Assert.AreEqual("MyErrorMessage", result.Response.Error?.Message);
        Assert.IsNotNull(result.Response.Error);
        Assert.IsTrue(result.Response.Error.Errors.ContainsKey("Field1"));
        Assert.AreEqual("ErrorField1_1", result.Response.Error.Errors["Field1"][0]);
        Assert.AreEqual("ErrorField1_2", result.Response.Error.Errors["Field1"][1]);
        Assert.IsInstanceOfType(result, typeof(ErrorResult));
        Assert.AreEqual(DeliveryStatus.Ack, result.DeliveryStatus);
        Assert.AreEqual(CarrotStatusCode.BadRequest, result.Response.StatusCode);
    }

    [TestMethod]
    public void HandlerResponse_InternalServerError_WithResponse_Check_ErrorResult_and_StatusCode()
    {
        var validationErrors = new Dictionary<string, string[]> { { "Field1", ["ErrorField1_1", "ErrorField1_2"] } };

        TestRequestHandler handler = new();
        Response response = new();

        var result = handler.Error(response, "MyErrorMessage", validationErrors);

        Assert.AreEqual(result.Response.Content, response);
        Assert.AreEqual("MyErrorMessage", result.Response.Error?.Message);
        Assert.IsNotNull(result.Response.Error);
        Assert.IsTrue(result.Response.Error.Errors.ContainsKey("Field1"));
        Assert.AreEqual("ErrorField1_1", result.Response.Error.Errors["Field1"][0]);
        Assert.AreEqual("ErrorField1_2", result.Response.Error.Errors["Field1"][1]);
        Assert.IsInstanceOfType(result, typeof(ErrorResult));
        Assert.AreEqual(DeliveryStatus.Ack, result.DeliveryStatus);
        Assert.AreEqual(CarrotStatusCode.InternalServerError, result.Response.StatusCode);
    }

    [TestMethod]
    public void HandlerResponse_InternalServerError_WithoutResponse_Check_ErrorResult_and_StatusCode()
    {
        var validationErrors = new Dictionary<string, string[]> { { "Field1", ["ErrorField1_1", "ErrorField1_2"] } };

        TestRequestHandler handler = new();

        var result = handler.Error("MyErrorMessage", validationErrors);

        Assert.IsNull(result.Response.Content);
        Assert.AreEqual("MyErrorMessage", result.Response.Error?.Message);
        Assert.IsNotNull(result.Response.Error);
        Assert.IsTrue(result.Response.Error.Errors.ContainsKey("Field1"));
        Assert.AreEqual("ErrorField1_1", result.Response.Error.Errors["Field1"][0]);
        Assert.AreEqual("ErrorField1_2", result.Response.Error.Errors["Field1"][1]);

        Assert.IsInstanceOfType(result, typeof(ErrorResult));
        Assert.AreEqual(DeliveryStatus.Ack, result.DeliveryStatus);
        Assert.AreEqual(CarrotStatusCode.InternalServerError, result.Response.StatusCode);
    }

    [TestMethod]
    public void HandlerResponse_CustomErrorCode_WithResponse_Check_ErrorResult_and_StatusCode()
    {
        const int customErrorCode = 23876;
        var validationErrors = new Dictionary<string, string[]> { { "Field1", ["ErrorField1_1", "ErrorField1_2"] } };

        TestRequestHandler handler = new();
        Response response = new();

        var result = handler.Error(customErrorCode, response, "MyErrorMessage", validationErrors);

        Assert.AreEqual(result.Response.Content, response);
        Assert.AreEqual("MyErrorMessage", result.Response.Error?.Message);
        Assert.IsNotNull(result.Response.Error);
        Assert.IsTrue(result.Response.Error.Errors.ContainsKey("Field1"));
        Assert.AreEqual("ErrorField1_1", result.Response.Error.Errors["Field1"][0]);
        Assert.AreEqual("ErrorField1_2", result.Response.Error.Errors["Field1"][1]);
        Assert.IsInstanceOfType(result, typeof(ErrorResult));
        Assert.AreEqual(DeliveryStatus.Ack, result.DeliveryStatus);
        Assert.AreEqual(customErrorCode, result.Response.StatusCode);
    }

    [TestMethod]
    public void HandlerResponse_Retry_WithResponse_Check_RetryResult_and_StatusCode()
    {
        TestRequestHandler handler = new();

        var result = handler.Retry();

        Assert.IsNull(result.Response.Content);
        Assert.IsNull(result.Response.Error);
        Assert.IsInstanceOfType(result, typeof(RetryResult));
        Assert.AreEqual(DeliveryStatus.Retry, result.DeliveryStatus);
        Assert.AreEqual(CarrotStatusCode.InternalServerError, result.Response.StatusCode);
    }

    [TestMethod]
    public void HandlerResponse_Reject_WithResponse_Check_RejectResult_and_StatusCode()
    {
        TestRequestHandler handler = new();

        var result = handler.Reject();

        Assert.IsNull(result.Response.Content);
        Assert.IsNull(result.Response.Error);
        Assert.IsInstanceOfType(result, typeof(RejectResult));
        Assert.AreEqual(DeliveryStatus.Reject, result.DeliveryStatus);
        Assert.AreEqual(CarrotStatusCode.InternalServerError, result.Response.StatusCode);
    }

    private class Response;

    // ReSharper disable once ClassNeverInstantiated.Local
    private class Request : IQuery<Request, Response, TestQueueEndPoint>;

    private class TestRequestHandler : RequestHandlerBase<Request, Response>
    {
        public override Task<IHandlerResult> HandleAsync(Request request, ConsumerContext consumerContext, CancellationToken cancellationToken)
        {
            return new OkResult().AsTask();
        }
    }
}