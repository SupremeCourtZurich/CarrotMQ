using System.Text.Json;
using CarrotMQ.Core.Dto;
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
public class RequestHandlerProcessorTest
{
    private const string ExceptionMessage = "test exception";
    private readonly ICarrotSerializer _serializer = new DefaultCarrotSerializer();

    private IDependencyInjector _dependencyInjector = null!;

    [TestInitialize]
    public void Setup()
    {
        _dependencyInjector = Substitute.For<IDependencyInjector>();

        _dependencyInjector.CreateAsyncScope().Returns(_dependencyInjector);
        _dependencyInjector.GetCarrotSerializer().Returns(_serializer);
        _dependencyInjector.GetMiddlewareProcessor().Returns(new MiddlewareProcessor(Array.Empty<ICarrotMiddleware>()));
    }

    [TestMethod]
    public async Task Handle_OkResult()
    {
        _dependencyInjector.CreateHandler<CommandHandler, TestCommand, TestResponse>().Returns(new CommandHandler());
        var handler = new RequestHandlerProcessor<CommandHandler, TestCommand, TestResponse>();
        var cmd = new TestCommand { RequestRunId = Guid.NewGuid() };
        CarrotMessage message = new(new CarrotHeader(), _serializer.Serialize(cmd));

        var middlewareContext = CreateTestMiddlewareContext(message);

        await handler.HandleAsync(middlewareContext, _dependencyInjector).ConfigureAwait(false);

        Assert.AreEqual(DeliveryStatus.Ack, middlewareContext.DeliveryStatus, nameof(DeliveryStatus));
        Assert.AreEqual(cmd.RequestRunId, (middlewareContext.HandlerResult?.Response.Content as TestResponse)?.ResponseRunId, nameof(DeliveryStatus));
    }

    [TestMethod]
    public async Task Handler_not_registered()
    {
        var messageHandlerType = typeof(CommandHandler);
        var exceptionMessage = $"No Handler of the type {messageHandlerType.Name} ({messageHandlerType.FullName}) could be instantiated";
        var handlerCaller = new RequestHandlerProcessor<CommandHandler, TestCommand, TestResponse>();
        CarrotMessage message = new(new CarrotHeader(), _serializer.Serialize(new TestCommand()));

        var middlewareContext = CreateTestMiddlewareContext(message);
        Exception? exception = null;

        try
        {
            await handlerCaller.HandleAsync(middlewareContext, _dependencyInjector);
        }
        catch (Exception e)
        {
            exception = e;
        }

        Assert.AreEqual(DeliveryStatus.Reject, middlewareContext.DeliveryStatus, nameof(DeliveryStatus));
        Assert.IsInstanceOfType<InvalidOperationException>(exception);
        Assert.AreEqual(exceptionMessage, exception.Message, "Exception.Message");
    }

    [TestMethod]
    public async Task MessagePayload_not_deserializable()
    {
        _dependencyInjector.CreateHandler<CommandHandler, TestCommand, TestResponse>().Returns(new CommandHandler());
        CarrotMessage request = new(new CarrotHeader(), "{bad payload}");
        var handlerCaller = new RequestHandlerProcessor<CommandHandler, TestCommand, TestResponse>();

        var middlewareContext = CreateTestMiddlewareContext(request);

        JsonException? exception = null;
        try
        {
            await handlerCaller.HandleAsync(middlewareContext, _dependencyInjector);
        }
        catch (JsonException e)
        {
            exception = e;
        }

        Assert.AreEqual(DeliveryStatus.Reject, middlewareContext.DeliveryStatus, nameof(DeliveryStatus));
        Assert.IsNotNull(exception, nameof(JsonException));
    }

    [TestMethod]
    public async Task MessagePayload_is_empty_string()
    {
        _dependencyInjector.CreateHandler<CommandHandler, TestCommand, TestResponse>().Returns(new CommandHandler());
        CarrotMessage request = new(new CarrotHeader(), string.Empty);
        var handlerCaller = new RequestHandlerProcessor<CommandHandler, TestCommand, TestResponse>();

        var middlewareContext = CreateTestMiddlewareContext(request);

        CarrotSerializerException? exception = null;
        try
        {
            await handlerCaller.HandleAsync(middlewareContext, _dependencyInjector);
        }
        catch (CarrotSerializerException e)
        {
            exception = e;
        }

        Assert.AreEqual(DeliveryStatus.Reject, middlewareContext.DeliveryStatus, nameof(DeliveryStatus));
        Assert.AreEqual($"Payload could not be deserialized into type {typeof(TestCommand).FullName}", exception?.Message, nameof(exception.Message));
        Assert.AreEqual(string.Empty, exception?.Payload, nameof(exception.Payload));
        Assert.AreEqual(typeof(TestCommand), exception?.TargetType, nameof(exception.TargetType));
    }

    [TestMethod]
    public async Task Unhandled_Exception()
    {
        _dependencyInjector.CreateHandler<ThrowExceptionHandler, TestCommand, TestResponse>().Returns(new ThrowExceptionHandler());
        var requestPayload = _serializer.Serialize(new TestCommand());
        var handlerCaller = new RequestHandlerProcessor<ThrowExceptionHandler, TestCommand, TestResponse>();
        CarrotMessage message = new(
            new CarrotHeader
            {
                ReplyExchange = "MyReplyExchange",
                ReplyRoutingKey = "RoutingKey"
            },
            requestPayload);

        var middlewareContext = CreateTestMiddlewareContext(message);

        Exception? exception = null;
        try
        {
            await handlerCaller.HandleAsync(
                middlewareContext,
                _dependencyInjector);
        }
        catch (Exception e)
        {
            exception = e;
        }

        Assert.AreEqual(DeliveryStatus.Reject, middlewareContext.DeliveryStatus, nameof(DeliveryStatus));
        Assert.AreEqual(ExceptionMessage, exception?.Message, "Exception.Message");
        Assert.AreEqual(
            $"Unhandled exception while handling message: {requestPayload}",
            middlewareContext.HandlerResult?.Response.Error?.Message,
            "CarrotError.Message");
    }

    private MiddlewareContext CreateTestMiddlewareContext(CarrotMessage message)
    {
        return new MiddlewareContext(message, typeof(TestCommand), TestConsumerContext.GetConsumerContext(), CancellationToken.None);
    }

    private class TestCommand : ICommand<TestCommand, TestResponse, TestQueueEndPoint>
    {
        public Guid RequestRunId { get; set; }
    }

    private class TestResponse
    {
        public Guid ResponseRunId { get; set; }
    }

    private class CommandHandler : CommandHandlerBase<TestCommand, TestResponse>
    {
        public override Task<IHandlerResult> HandleAsync(TestCommand command, ConsumerContext consumerContext, CancellationToken cancellationToken)
        {
            return Ok(new TestResponse { ResponseRunId = command.RequestRunId }).AsTask();
        }
    }

    private class ThrowExceptionHandler : CommandHandlerBase<TestCommand, TestResponse>
    {
        public override Task<IHandlerResult> HandleAsync(TestCommand command, ConsumerContext consumerContext, CancellationToken cancellationToken)
        {
            throw new Exception(ExceptionMessage);
        }
    }
}