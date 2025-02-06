using CarrotMQ.Core.Configuration;
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
using CarrotMQ.Core.Tracing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace CarrotMQ.Core.Test.MessageProcessing;

[TestClass]
public class MessageDistributorTest
{
    private static List<TestCommand> s_handledMessages = new();
    private readonly ICarrotSerializer _carrotSerializer = new DefaultCarrotSerializer();

    private IDependencyInjector _dependencyInjector = null!;
    private HandlerCollection _handlerConfiguration = null!;
    private IMessageDistributor _messageDistributor = null!;

    [TestInitialize]
    public void Setup()
    {
        s_handledMessages = new List<TestCommand>();

        _dependencyInjector = Substitute.For<IDependencyInjector>();
        _dependencyInjector.CreateAsyncScope().Returns(_dependencyInjector);
        _dependencyInjector.GetCarrotSerializer().Returns(_carrotSerializer);
        _dependencyInjector.GetMiddlewareProcessor().Returns(new MiddlewareProcessor(Array.Empty<ICarrotMiddleware>()));

        _handlerConfiguration = new HandlerCollection(new ServiceCollection(), new BindingCollection());
        _messageDistributor = new MessageDistributor(
            _dependencyInjector,
            _handlerConfiguration,
            new ResponseSender(
                Substitute.For<ITransport>(),
                _carrotSerializer,
                Substitute.For<ICarrotMetricsRecorder>(),
                TestLoggerFactory.CreateLogger<ResponseSender>()),
            TestLoggerFactory.CreateLogger<MessageDistributor>());
    }

    [TestMethod]
    public async Task Command_processor()
    {
        _handlerConfiguration.AddCommand<BounceCommandHandler, TestCommand, TestResponse>();
        _dependencyInjector.CreateHandler<BounceCommandHandler, TestCommand, TestResponse>().Returns(new BounceCommandHandler());
        var runId = Guid.NewGuid();
        var cmd = new TestCommand { RequestRunId = runId };
        var requestCarrotHeader = new CarrotHeader
        {
            CalledMethod = typeof(TestCommand).FullName!,
            ReplyRoutingKey = "MyRoutingKey"
        };
        CarrotMessage message = new(requestCarrotHeader, _carrotSerializer.Serialize(cmd));

        var deliveryStatus = await _messageDistributor.DistributeAsync(message, CancellationToken.None);

        Assert.AreEqual(DeliveryStatus.Ack, deliveryStatus, nameof(DeliveryStatus));
        Assert.AreEqual(1, s_handledMessages.Count, "handled messages count");
        Assert.AreEqual(runId, s_handledMessages.First().RequestRunId, "dto id");
    }

    [TestMethod]
    public async Task Command_response_processor()
    {
        _handlerConfiguration.AddResponse<ResponseHandler, TestCommand, TestResponse>();
        _dependencyInjector.CreateHandler<ResponseHandler, CarrotResponse<TestCommand, TestResponse>, NoResponse>().Returns(new ResponseHandler());
        var header = new CarrotHeader
        {
            CalledMethod = "Response:" + typeof(TestCommand).FullName,
            ReplyRoutingKey = "MyRoutingKey",
            IncludeRequestPayloadInResponse = true
        };
        _carrotSerializer.Serialize(new TestCommand());

        var responsePayload = _carrotSerializer.Serialize(
            new CarrotResponse
            {
                Content = new TestResponse(),
                StatusCode = CarrotStatusCode.Ok
            });
        CarrotMessage message = new(header, responsePayload);

        var deliveryStatus = await _messageDistributor.DistributeAsync(message, CancellationToken.None);

        Assert.AreEqual(DeliveryStatus.Ack, deliveryStatus, nameof(DeliveryStatus));
        Assert.AreEqual(1, s_handledMessages.Count, "handled messages count");
    }

    [TestMethod]
    public async Task Timeout_by_global_Max_Processing_Time()
    {
        _handlerConfiguration.AddCommand<EndlessCommandHandler, TestCommand, TestResponse>();
        _dependencyInjector.CreateHandler<EndlessCommandHandler, TestCommand, TestResponse>().Returns(new EndlessCommandHandler());
        var header = new CarrotHeader
        {
            CalledMethod = typeof(TestCommand).FullName!,
            ReplyRoutingKey = "MyRoutingKey"
        };
        var cmd = new TestCommand();
        CarrotMessage message = new(header, _carrotSerializer.Serialize(cmd));
        using var cts = new CancellationTokenSource(50);

        var deliveryStatus = await _messageDistributor.DistributeAsync(message, cts.Token).ConfigureAwait(false);

        Assert.AreEqual(DeliveryStatus.Ack, deliveryStatus, nameof(DeliveryStatus));
        Assert.AreEqual(1, s_handledMessages.Count, "handled messages count");
    }

    [TestMethod]
    public async Task Timeout_by_Context_Ttl()
    {
        _handlerConfiguration.AddCommand<EndlessCommandHandler, TestCommand, TestResponse>();
        _dependencyInjector.CreateHandler<EndlessCommandHandler, TestCommand, TestResponse>().Returns(new EndlessCommandHandler());
        var header = new CarrotHeader
        {
            CalledMethod = typeof(TestCommand).FullName!,
            ReplyRoutingKey = "MyRoutingKey",
            MessageProperties = new MessageProperties { Ttl = 50 }
        };
        var cmd = new TestCommand();
        CarrotMessage message = new(header, _carrotSerializer.Serialize(cmd));

        var deliveryStatus = await _messageDistributor.DistributeAsync(message, CancellationToken.None).ConfigureAwait(false);

        Assert.AreEqual(DeliveryStatus.Ack, deliveryStatus, nameof(DeliveryStatus));
        Assert.AreEqual(1, s_handledMessages.Count, "handled messages count");
    }

    public class TestCommand : ICommand<TestCommand, TestResponse, TestQueueEndPoint>
    {
        public Guid RequestRunId { get; set; }
    }

    public class TestResponse
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public Guid ResponseRunId { get; set; }
    }

    public sealed class BounceCommandHandler : CommandHandlerBase<TestCommand, TestResponse>
    {
        public override Task<IHandlerResult> HandleAsync(TestCommand command, ConsumerContext consumerContext, CancellationToken cancellationToken)
        {
            s_handledMessages.Add(command);

            return Ok(new TestResponse { ResponseRunId = command.RequestRunId }).AsTask();
        }
    }

    public sealed class ResponseHandler : ResponseHandlerBase<TestCommand, TestResponse>
    {
        public override Task<IHandlerResult> HandleAsync(
            CarrotResponse<TestCommand, TestResponse> carrotResponse,
            ConsumerContext consumerContext,
            CancellationToken cancellationToken)
        {
            s_handledMessages.Add(carrotResponse.Request!);

            return Ok().AsTask();
        }
    }

    public sealed class EndlessCommandHandler : CommandHandlerBase<TestCommand, TestResponse>
    {
        public override async Task<IHandlerResult> HandleAsync(
            TestCommand command,
            ConsumerContext consumerContext,
            CancellationToken cancellationToken)
        {
            s_handledMessages.Add(command);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return Error(CarrotStatusCode.GatewayTimeout);
            }

            return Ok(new TestResponse { ResponseRunId = command.RequestRunId });
        }
    }
}