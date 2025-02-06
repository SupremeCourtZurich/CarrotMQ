using CarrotMQ.Core.Dto;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.MessageProcessing.Middleware;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;
using CarrotMQ.Core.Test.Helper;
using NSubstitute;

namespace CarrotMQ.Core.Test.MessageProcessing;

[TestClass]
public class ResponseHandlerProcessorTest
{
    private static CarrotResponse<TestCommand, TestResponse>? s_response;
    private IDependencyInjector _dependencyInjector = null!;
    private ICarrotSerializer _serializer = null!;

    [TestInitialize]
    public void Setup()
    {
        _serializer = new DefaultCarrotSerializer();
        _dependencyInjector = Substitute.For<IDependencyInjector>();
        _dependencyInjector.GetCarrotSerializer().Returns(_serializer);

        _dependencyInjector.CreateHandler<ResponseHandler, CarrotResponse<TestCommand, TestResponse>, NoResponse>().Returns(new ResponseHandler());
    }

    [TestMethod]
    public async Task Handle_ok()
    {
        var request = new TestCommand();
        var response = new CarrotResponse<TestCommand, TestResponse>();
        var payload = _serializer.Serialize(
            new CarrotResponse
            {
                Content = response,
                Request = request,
                StatusCode = CarrotStatusCode.Ok
            });

        var header = new CarrotHeader { IncludeRequestPayloadInResponse = true };
        var message = new CarrotMessage(header, payload);

        var handlerCaller = new ResponseHandlerProcessor<ResponseHandler, TestCommand, TestResponse>();

        var middlewareContext = CreateTestMiddlewareContext(message, header);

        await handlerCaller.HandleAsync(middlewareContext, _dependencyInjector)
            .ConfigureAwait(false);

        var deliveryStatus = middlewareContext.DeliveryStatus;

        Assert.IsNotNull(s_response);
        Assert.AreEqual(DeliveryStatus.Ack, deliveryStatus, nameof(DeliveryStatus));
        Assert.AreEqual(CarrotStatusCode.Ok, s_response.StatusCode, nameof(CarrotStatusCode));
    }

    [TestMethod]
    public async Task Handle_empty_response()
    {
        var header = new CarrotHeader { IncludeRequestPayloadInResponse = true };
        var message = new CarrotMessage(header, _serializer.Serialize(new TestCommand()));

        var handlerCaller = new ResponseHandlerProcessor<ResponseHandler, TestCommand, TestResponse>();

        var middlewareContext = CreateTestMiddlewareContext(message, header);

        await handlerCaller.HandleAsync(middlewareContext, _dependencyInjector)
            .ConfigureAwait(false);

        var deliveryStatus = middlewareContext.DeliveryStatus;

        Assert.IsNotNull(s_response);
        Assert.AreEqual(DeliveryStatus.Ack, deliveryStatus, nameof(DeliveryStatus));
        Assert.AreEqual(0, s_response.StatusCode, nameof(s_response.StatusCode));
        Assert.IsNull(s_response.Error, nameof(s_response.Error));
    }

    [TestMethod]
    public async Task Handle_Error_Response()
    {
        const string errorMsg = "test error";
        var request = new TestCommand();
        var response = new CarrotResponse<TestCommand, TestResponse>
        {
            Request = request,
            StatusCode = CarrotStatusCode.BadRequest,
            Error = new CarrotError(errorMsg)
        };
        var responsePayload = _serializer.Serialize(response);
        var header = new CarrotHeader { IncludeRequestPayloadInResponse = true };
        var message = new CarrotMessage(header, responsePayload);

        var handlerCaller = new ResponseHandlerProcessor<ResponseHandler, TestCommand, TestResponse>();

        var middlewareContext = CreateTestMiddlewareContext(message, header);

        await handlerCaller.HandleAsync(middlewareContext, _dependencyInjector);

        var deliveryStatus = middlewareContext.DeliveryStatus;

        Assert.IsNotNull(s_response);
        Assert.AreEqual(DeliveryStatus.Ack, deliveryStatus, nameof(DeliveryStatus));
        Assert.AreEqual(CarrotStatusCode.BadRequest, s_response.StatusCode, nameof(s_response.StatusCode));
        Assert.AreEqual(errorMsg, s_response.Error?.Message, "error message");
    }

    private MiddlewareContext CreateTestMiddlewareContext(CarrotMessage message, CarrotHeader header)
    {
        return new MiddlewareContext(message, typeof(TestCommand), TestConsumerContext.CreateConsumerContext(header), CancellationToken.None);
    }

    public class TestCommand : ICommand<TestCommand, TestResponse, TestQueueEndPoint>;

    public class TestResponse;

    public class ResponseHandler : ResponseHandlerBase<TestCommand, TestResponse>
    {
        public override Task<IHandlerResult> HandleAsync(
            CarrotResponse<TestCommand, TestResponse> response,
            ConsumerContext consumerContext,
            CancellationToken cancellationToken)
        {
            s_response = response;

            return Ok().AsTask();
        }
    }
}