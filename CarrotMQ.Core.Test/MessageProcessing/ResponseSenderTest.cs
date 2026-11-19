using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.MessageProcessing.Middleware;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;
using CarrotMQ.Core.Telemetry;
using CarrotMQ.Core.Test.Helper;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CarrotMQ.Core.Test.MessageProcessing;

[TestClass]
public class ResponseSenderTest
{
    private readonly List<CarrotMessage> _publishedMessages = new();
    private ResponseSenderLogger _logger = null!;
    private IResponseSender _responseSender = null!;
    private ITransport _transport = null!;

    [TestInitialize]
    public void Setup()
    {
        _transport = Substitute.For<ITransport>();
        _transport.SendAsync(Arg.Do<CarrotMessage>(x => _publishedMessages.Add(x)), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(Task.CompletedTask);

        _logger = new ResponseSenderLogger();

        _responseSender = new ResponseSender(
            _transport,
            new DefaultCarrotSerializer(),
            Substitute.For<ICarrotMetricsRecorder>(),
            _logger);
    }

    [TestMethod]
    public async Task ResponseSender_Sends_Response()
    {
        var header = new CarrotHeader
        {
            ReplyExchange = "ReplyExchange",
            ReplyRoutingKey = "ReplyRoutingKey",
            CalledMethod = "CalledMethod",
            CustomHeader = new Dictionary<string, string>(StringComparer.Ordinal) { { "key", "value" } },
            CorrelationId = Guid.NewGuid(),
            MessageProperties = new MessageProperties
            {
                Persistent = true,
                Ttl = 123,
                Priority = 4,
                PublisherConfirm = false
            },
            MessageId = Guid.NewGuid(),
            InitialUserName = "InitialUserName",
            InitialServiceName = "InitialServiceName",
            IncludeRequestPayloadInResponse = true
        };
        var message = new CarrotMessage(header, "payload");
        var middlewareContext = CreateTestMiddlewareContext(message);
        middlewareContext.HandlerResult = new OkResult();
        middlewareContext.ResponseRequired = true;
        middlewareContext.ResponseSent = false;

        await _responseSender.TrySendResponseAsync(middlewareContext).ConfigureAwait(false);
        Assert.HasCount(1, _publishedMessages);
        var publishedMessage = _publishedMessages.First();
        var replyHeader = publishedMessage.Header;
        Assert.AreEqual(CalledMethodResolver.BuildResponseCalledMethodKey(message.Header.CalledMethod), replyHeader.CalledMethod);
        Assert.AreEqual(header.ReplyRoutingKey, replyHeader.RoutingKey);
        Assert.AreEqual(header.CustomHeader["key"], replyHeader.CustomHeader?["key"]);
        Assert.AreEqual(header.CorrelationId, replyHeader.CorrelationId);
        Assert.AreEqual(header.MessageProperties, replyHeader.MessageProperties);
        Assert.AreEqual(header.InitialUserName, replyHeader.InitialUserName);
        Assert.AreEqual(header.InitialServiceName, replyHeader.InitialServiceName);
        Assert.AreEqual(header.IncludeRequestPayloadInResponse, replyHeader.IncludeRequestPayloadInResponse);
    }

    [TestMethod]
    public async Task ResponseSender_Response_Already_Sent()
    {
        var message = new CarrotMessage(new CarrotHeader(), "payload");
        var middlewareContext = CreateTestMiddlewareContext(message);
        middlewareContext.HandlerResult = new OkResult();
        middlewareContext.ResponseRequired = true;
        middlewareContext.ResponseSent = true;

        await _responseSender.TrySendResponseAsync(middlewareContext).ConfigureAwait(false);
        Assert.IsEmpty(_publishedMessages);
        Assert.IsEmpty(_logger.LogMessages);
    }

    [TestMethod]
    public async Task ResponseSender_ErrorResponse_Can_Not_Be_Sent_Is_Logged()
    {
        var message = new CarrotMessage(new CarrotHeader(), "payload");
        var middlewareContext = CreateTestMiddlewareContext(message);
        middlewareContext.HandlerResult = new ErrorResult(null, null);
        middlewareContext.ResponseRequired = false;
        middlewareContext.ResponseSent = true;

        await _responseSender.TrySendResponseAsync(middlewareContext).ConfigureAwait(false);
        Assert.IsEmpty(_publishedMessages);

        Assert.AreEqual(1, _logger.LogMessages.Count(m => m == LogLevel.Warning));
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ResponseSender_TtlPassesWhilePublishing_DropsTheResponseWithAWarning()
    {
        // Arrange
        _transport.SendAsync(Arg.Any<CarrotMessage>(), Arg.Any<CancellationToken>())
            .Returns(ci => FailWhenCancelledAsync(ci.Arg<CancellationToken>()));
        var header = new CarrotHeader { CorrelationId = Guid.NewGuid(), MessageProperties = new MessageProperties { Ttl = 50 } };
        var middlewareContext = CreateTestMiddlewareContext(new CarrotMessage(header, "payload"));
        middlewareContext.HandlerResult = new OkResult();
        middlewareContext.ResponseRequired = true;

        // Act
        await _responseSender.TrySendResponseAsync(middlewareContext).ConfigureAwait(false);

        // Assert
        Assert.AreEqual(1, _logger.LogMessages.Count(m => m == LogLevel.Warning));
    }
    [TestMethod]
    [Timeout(5_000)]
    public async Task ResponseSender_RetryLimitExceededAsTheTtlPasses_Propagates()
    {
        // Arrange
        _transport.SendAsync(Arg.Any<CarrotMessage>(), Arg.Any<CancellationToken>())
            .Returns(ci => RetryLimitExceededWhenCancelledAsync(ci.Arg<CancellationToken>()));
        var header = new CarrotHeader { CorrelationId = Guid.NewGuid(), MessageProperties = new MessageProperties { Ttl = 50 } };
        var middlewareContext = CreateTestMiddlewareContext(new CarrotMessage(header, "payload"));
        middlewareContext.HandlerResult = new OkResult();
        middlewareContext.ResponseRequired = true;

        // Act
        var send = _responseSender.TrySendResponseAsync(middlewareContext);

        // Assert
        await Assert.ThrowsExactlyAsync<RetryLimitExceededException>(() => send).ConfigureAwait(false);
    }

    private static async Task RetryLimitExceededWhenCancelledAsync(CancellationToken token)
    {
        await Task.Delay(Timeout.Infinite, token).ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);

        throw new RetryLimitExceededException("no confirm", PublishFailureReason.Unknown);
    }
    private MiddlewareContext CreateTestMiddlewareContext(CarrotMessage message)
    {
        return new MiddlewareContext(message, typeof(object), TestConsumerContext.GetConsumerContext(), CancellationToken.None);
    }

    // Like a transport waiting for an unreachable broker: it gives up only when its token fires.
    private static async Task FailWhenCancelledAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            throw new PublishFailedException("cancelled", PublishFailureReason.Unreachable, ex);
        }
    }
    public class ResponseSenderLogger : ILogger<ResponseSender>
    {
        public IList<LogLevel> LogMessages { get; } = [];

        void ILogger.Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Log(logLevel, formatter(state, exception));

        public void Log(LogLevel logLevel, string message)
        {
            LogMessages.Add(logLevel);
        }

        public virtual bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
    }
}
