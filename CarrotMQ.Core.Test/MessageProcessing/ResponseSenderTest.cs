using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.MessageProcessing.Middleware;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;
using CarrotMQ.Core.Test.Helper;
using CarrotMQ.Core.Tracing;
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
            CustomHeader = new Dictionary<string, string> { { "key", "value" } },
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
        Assert.AreEqual(1, _publishedMessages.Count);
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
        Assert.AreEqual(0, _publishedMessages.Count);
        Assert.AreEqual(0, _logger.LogMessages.Count);
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
        Assert.AreEqual(0, _publishedMessages.Count);

        Assert.AreEqual(1, _logger.LogMessages.Count(m => m == LogLevel.Warning));
    }

    private MiddlewareContext CreateTestMiddlewareContext(CarrotMessage message)
    {
        return new MiddlewareContext(message, typeof(object), TestConsumerContext.GetConsumerContext(), CancellationToken.None);
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