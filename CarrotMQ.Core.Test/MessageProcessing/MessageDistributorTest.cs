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
using CarrotMQ.Core.Telemetry;
using CarrotMQ.Core.Test.Helper;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CarrotMQ.Core.Test.MessageProcessing;

[TestClass]
public class MessageDistributorTest
{
    private static List<TestCommand> s_handledMessages = new();
    private static TimeSpan s_handlerDelay;
    private static TaskCompletionSource<bool> s_handlerStarted = new();
    private readonly ICarrotSerializer _carrotSerializer = new DefaultCarrotSerializer();

    private IDependencyInjector _dependencyInjector = null!;
    private HandlerCollection _handlerConfiguration = null!;
    private IMessageDistributor _messageDistributor = null!;

    [TestInitialize]
    public void Setup()
    {
        s_handledMessages = new List<TestCommand>();
        s_handlerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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
        Assert.HasCount(1, s_handledMessages, "handled messages count");
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
        Assert.HasCount(1, s_handledMessages, "handled messages count");
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
        Assert.HasCount(1, s_handledMessages, "handled messages count");
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
        Assert.HasCount(1, s_handledMessages, "handled messages count");
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task HandlerTakesPartOfTheTtl_ResponseTokenFiresAtTheTtlCountedFromArrival()
    {
        // Arrange
        const int ttl = 2_000;
        s_handlerDelay = TimeSpan.FromMilliseconds(ttl * 0.6);
        var (distributor, _, cancelledAtCall) = CreateDistributorWithResponseSenderSpy(waitForCancellation: true);
        _handlerConfiguration.AddCommand<DelayCommandHandler, TestCommand, TestResponse>();
        _dependencyInjector.CreateHandler<DelayCommandHandler, TestCommand, TestResponse>().Returns(new DelayCommandHandler());
        var stopwatch = Stopwatch.StartNew();

        // Act
        await distributor.DistributeAsync(CreateRequest(ttl), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.IsFalse(cancelledAtCall(), "The response token was already cancelled when the response was sent.");
        Assert.IsLessThan(ttl * 1.4, stopwatch.ElapsedMilliseconds, "The response token fired long after the TTL.");
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task HandlerRunsPastTheTtl_ResponseIsDroppedWithAWarning()
    {
        // Arrange
        var transport = Substitute.For<ITransport>();
        CancellationToken sentWith = default;
        transport.SendAsync(Arg.Any<CarrotMessage>(), Arg.Do<CancellationToken>(t => sentWith = t))
            .Returns(ci => FailWhenCancelledAsync(ci.Arg<CancellationToken>()));
        var logger = new ResponseSenderTest.ResponseSenderLogger();
        var distributor = new MessageDistributor(
            _dependencyInjector,
            _handlerConfiguration,
            new ResponseSender(transport, _carrotSerializer, Substitute.For<ICarrotMetricsRecorder>(), logger),
            TestLoggerFactory.CreateLogger<MessageDistributor>());
        _handlerConfiguration.AddCommand<EndlessCommandHandler, TestCommand, TestResponse>();
        _dependencyInjector.CreateHandler<EndlessCommandHandler, TestCommand, TestResponse>().Returns(new EndlessCommandHandler());

        // Act
        var deliveryStatus = await distributor.DistributeAsync(CreateRequest(50), CancellationToken.None).ConfigureAwait(false);

        // Assert
        Assert.AreEqual(DeliveryStatus.Ack, deliveryStatus);
        Assert.IsTrue(sentWith.IsCancellationRequested, "The response was sent with a token that outlived the TTL.");
        Assert.AreEqual(1, logger.LogMessages.Count(l => l == LogLevel.Warning));
    }

    [TestMethod]
    [Timeout(5_000)]
    [DataRow(null)]
    [DataRow(0)]
    public async Task NoPositiveTtlAndTheConsumerCancelsTheHandler_ResponseIsStillSentWithinABound(int? ttl)
    {
        // Arrange
        var (distributor, responseToken, cancelledAtCall) = CreateDistributorWithResponseSenderSpy();
        _handlerConfiguration.AddCommand<EndlessCommandHandler, TestCommand, TestResponse>();
        _dependencyInjector.CreateHandler<EndlessCommandHandler, TestCommand, TestResponse>().Returns(new EndlessCommandHandler());
        using var consumerCts = new CancellationTokenSource();
        var distribute = distributor.DistributeAsync(CreateRequest(ttl), consumerCts.Token);
        await s_handlerStarted.Task.ConfigureAwait(false);

        // Act
        consumerCts.Cancel();
        await distribute.ConfigureAwait(false);

        // Assert
        Assert.IsFalse(cancelledAtCall(), "The response token was cancelled with the handler.");
        Assert.IsTrue(responseToken().CanBeCanceled, "The response publish has no bound.");
    }

    // With waitForCancellation the spy sends like a transport facing an unreachable broker: until its token fires.
    private (MessageDistributor Distributor, Func<CancellationToken> ResponseToken, Func<bool> CancelledAtCall) CreateDistributorWithResponseSenderSpy(
        bool waitForCancellation = false)
    {
        var responseSender = Substitute.For<IResponseSender>();
        CancellationToken responseToken = default;
        var cancelledAtCall = false;
        responseSender.TrySendResponseAsync(
                Arg.Any<MiddlewareContext>(),
                Arg.Do<CancellationToken>(t =>
                {
                    responseToken = t;
                    cancelledAtCall = t.IsCancellationRequested;
                }))
            .Returns(ci => waitForCancellation ? WaitForCancellationAsync(ci.Arg<CancellationToken>()) : Task.CompletedTask);
        var distributor = new MessageDistributor(
            _dependencyInjector,
            _handlerConfiguration,
            responseSender,
            TestLoggerFactory.CreateLogger<MessageDistributor>());

        return (distributor, () => responseToken, () => cancelledAtCall);
    }

    private CarrotMessage CreateRequest(int? ttl)
    {
        var header = new CarrotHeader
        {
            CalledMethod = typeof(TestCommand).FullName!,
            ReplyRoutingKey = "MyRoutingKey",
            MessageProperties = new MessageProperties { Ttl = ttl }
        };

        return new CarrotMessage(header, _carrotSerializer.Serialize(new TestCommand()));
    }

    private static async Task WaitForCancellationAsync(CancellationToken token)
    {
        await Task.Delay(Timeout.Infinite, token).ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
    }

    private static async Task FailWhenCancelledAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            throw new OperationCanceledException("cancelled", new PublishFailedException("cancelled", PublishFailureReason.Unknown, ex), token);
        }
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

    public sealed class DelayCommandHandler : CommandHandlerBase<TestCommand, TestResponse>
    {
        public override async Task<IHandlerResult> HandleAsync(
            TestCommand command,
            ConsumerContext consumerContext,
            CancellationToken cancellationToken)
        {
            s_handledMessages.Add(command);
            await Task.Delay(s_handlerDelay, cancellationToken).ConfigureAwait(false);

            return Ok(new TestResponse { ResponseRunId = command.RequestRunId });
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
            s_handlerStarted.TrySetResult(true);
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