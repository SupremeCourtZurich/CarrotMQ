using CarrotMQ.Core.Dto;
using CarrotMQ.Core.EndPoints;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.MessageSending;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;
using CarrotMQ.Core.Test.Helper;
using NSubstitute;

namespace CarrotMQ.Core.Test;

[TestClass]
public class CarrotClientHeaderTests
{
    private const string UserName = "John";
    private const string ServiceName = "MyMicroService";
    private readonly ICommand<TestDto, TestResponse, TestQueueEndPoint> _commandDto = new TestDto(1);
    private readonly IEvent<TestDto, TestExchangeEndPoint> _eventDto = new TestDto(1);
    private readonly IQuery<TestDto, TestResponse, TestQueueEndPoint> _queryDto = new TestDto(1);

    private ICarrotClient _carrotClient = null!;
    private IDependencyInjector _dependencyInjector = null!;
    private CarrotMessage _resultingMessage = null!;
    private ITransport _transport = null!;

    [TestInitialize]
    public void Initialize()
    {
        _transport = Substitute.For<ITransport>();
        _transport.SendAsync(Arg.Any<CarrotMessage>(), CancellationToken.None)
            .ReturnsForAnyArgs(
                x =>
                {
                    _resultingMessage = x.Arg<CarrotMessage>();

                    return Task.FromResult(new CarrotMessage());
                });
        _transport.SendReceiveAsync(Arg.Any<CarrotMessage>(), CancellationToken.None)
            .ReturnsForAnyArgs(
                x =>
                {
                    _resultingMessage = x.Arg<CarrotMessage>();

                    return Task.FromResult(new CarrotMessage { Payload = "{}" });
                });

        _dependencyInjector = Substitute.For<IDependencyInjector>();
        _dependencyInjector.CreateAsyncScope().Returns(_ => _dependencyInjector);
        var serializer = new DefaultCarrotSerializer();
        var messageBuilder = new CarrotMessageBuilder([], serializer, new DefaultRoutingKeyResolver());
        _carrotClient = new CarrotClient(_transport, serializer, messageBuilder);
    }

    [TestCleanup]
    public void CleanUp()
    {
        Assert.AreEqual(1, _transport.ReceivedCalls().Count());
    }

    [TestMethod]
    public async Task Publish()
    {
        await _carrotClient.PublishAsync(_eventDto);

        CarrotHeader resultHeader = _resultingMessage.Header;
        AssertGeneralPublishCarrotHeaderProperties(resultHeader);
        Assert.IsNull(resultHeader.InitialUserName, nameof(resultHeader.InitialUserName));
        Assert.IsNull(resultHeader.InitialServiceName, nameof(resultHeader.InitialServiceName));
        Assert.IsTrue(resultHeader.MessageProperties.PublisherConfirm, nameof(resultHeader.MessageProperties.PublisherConfirm));
        Assert.AreEqual(null, resultHeader.MessageProperties.Ttl, nameof(resultHeader.MessageProperties.Ttl));
        Assert.AreEqual(0, resultHeader.MessageProperties.Priority, nameof(resultHeader.MessageProperties.Priority));
        Assert.IsFalse(resultHeader.MessageProperties.Persistent, nameof(resultHeader.MessageProperties.Persistent));
    }

    [TestMethod]
    public async Task PublishWithContext()
    {
        var context = new Context();

        await _carrotClient.PublishAsync(_eventDto, context);

        CarrotHeader resultHeader = _resultingMessage.Header;
        AssertGeneralPublishCarrotHeaderProperties(resultHeader);
        Assert.IsNull(resultHeader.InitialUserName, nameof(resultHeader.InitialUserName));
        Assert.IsNull(resultHeader.InitialServiceName, nameof(resultHeader.InitialServiceName));
        Assert.IsTrue(resultHeader.MessageProperties.PublisherConfirm, nameof(resultHeader.MessageProperties.PublisherConfirm));
        Assert.AreEqual(null, resultHeader.MessageProperties.Ttl, nameof(resultHeader.MessageProperties.Ttl));
        Assert.AreEqual(0, resultHeader.MessageProperties.Priority, nameof(resultHeader.MessageProperties.Priority));
        Assert.IsFalse(resultHeader.MessageProperties.Persistent, nameof(resultHeader.MessageProperties.Persistent));
    }

    [TestMethod]
    public async Task PublishWithInitialUserName()
    {
        var context = new Context(UserName);

        await _carrotClient.PublishAsync(_eventDto, context);

        CarrotHeader resultHeader = _resultingMessage.Header;
        AssertGeneralPublishCarrotHeaderProperties(resultHeader);
        Assert.AreEqual(UserName, resultHeader.InitialUserName, nameof(resultHeader.InitialUserName));
        Assert.IsNull(resultHeader.InitialServiceName, nameof(resultHeader.InitialServiceName));
        Assert.IsTrue(resultHeader.MessageProperties.PublisherConfirm, nameof(resultHeader.MessageProperties.PublisherConfirm));
        Assert.AreEqual(null, resultHeader.MessageProperties.Ttl, nameof(resultHeader.MessageProperties.Ttl));
        Assert.AreEqual(0, resultHeader.MessageProperties.Priority, nameof(resultHeader.MessageProperties.Priority));
        Assert.IsFalse(resultHeader.MessageProperties.Persistent, nameof(resultHeader.MessageProperties.Persistent));
    }

    [TestMethod]
    public async Task PublishWithInitialServiceName()
    {
        var context = new Context(initialServiceName: ServiceName);

        await _carrotClient.PublishAsync(_eventDto, context);

        CarrotHeader resultHeader = _resultingMessage.Header;
        AssertGeneralPublishCarrotHeaderProperties(resultHeader);
        Assert.IsNull(resultHeader.InitialUserName, nameof(resultHeader.InitialUserName));
        Assert.AreEqual(ServiceName, resultHeader.InitialServiceName, nameof(resultHeader.InitialServiceName));
        Assert.IsTrue(resultHeader.MessageProperties.PublisherConfirm, nameof(resultHeader.MessageProperties.PublisherConfirm));
        Assert.AreEqual(null, resultHeader.MessageProperties.Ttl, nameof(resultHeader.MessageProperties.Ttl));
        Assert.AreEqual(0, resultHeader.MessageProperties.Priority, nameof(resultHeader.MessageProperties.Priority));
        Assert.IsFalse(resultHeader.MessageProperties.Persistent, nameof(resultHeader.MessageProperties.Persistent));
    }

    [TestMethod]
    public async Task PublishWithoutConfirm()
    {
        await _carrotClient.PublishAsync(_eventDto, messageProperties: new MessageProperties { PublisherConfirm = false });

        CarrotHeader resultHeader = _resultingMessage.Header;
        AssertGeneralPublishCarrotHeaderProperties(resultHeader);
        Assert.IsNull(resultHeader.InitialUserName, nameof(resultHeader.InitialUserName));
        Assert.IsNull(resultHeader.InitialServiceName, nameof(resultHeader.InitialServiceName));
        Assert.IsFalse(resultHeader.MessageProperties.PublisherConfirm, nameof(resultHeader.MessageProperties.PublisherConfirm));
        Assert.AreEqual(null, resultHeader.MessageProperties.Ttl, nameof(resultHeader.MessageProperties.Ttl));
        Assert.AreEqual(0, resultHeader.MessageProperties.Priority, nameof(resultHeader.MessageProperties.Priority));
        Assert.IsFalse(resultHeader.MessageProperties.Persistent, nameof(resultHeader.MessageProperties.Persistent));
    }

    [DataRow(2_000)]
    [DataRow(0)]
    [TestMethod]
    public async Task PublishWithTtl(int ttl)
    {
        await _carrotClient.PublishAsync(_eventDto, messageProperties: new MessageProperties { Ttl = ttl });

        CarrotHeader resultHeader = _resultingMessage.Header;
        AssertGeneralPublishCarrotHeaderProperties(resultHeader);
        Assert.IsNull(resultHeader.InitialUserName, nameof(resultHeader.InitialUserName));
        Assert.IsNull(resultHeader.InitialServiceName, nameof(resultHeader.InitialServiceName));
        Assert.IsTrue(resultHeader.MessageProperties.PublisherConfirm, nameof(resultHeader.MessageProperties.PublisherConfirm));
        Assert.AreEqual(ttl, resultHeader.MessageProperties.Ttl, nameof(resultHeader.MessageProperties.Ttl));
        Assert.AreEqual(0, resultHeader.MessageProperties.Priority, nameof(resultHeader.MessageProperties.Priority));
        Assert.IsFalse(resultHeader.MessageProperties.Persistent, nameof(resultHeader.MessageProperties.Persistent));
    }

    [TestMethod]
    public async Task PublishWithPriority()
    {
        await _carrotClient.PublishAsync(_eventDto, messageProperties: new MessageProperties { Priority = 2 });

        CarrotHeader resultHeader = _resultingMessage.Header;
        AssertGeneralPublishCarrotHeaderProperties(resultHeader);
        Assert.IsNull(resultHeader.InitialUserName, nameof(resultHeader.InitialUserName));
        Assert.IsNull(resultHeader.InitialServiceName, nameof(resultHeader.InitialServiceName));
        Assert.IsTrue(resultHeader.MessageProperties.PublisherConfirm, nameof(resultHeader.MessageProperties.PublisherConfirm));
        Assert.AreEqual(null, resultHeader.MessageProperties.Ttl, nameof(resultHeader.MessageProperties.Ttl));
        Assert.AreEqual(2, resultHeader.MessageProperties.Priority, nameof(resultHeader.MessageProperties.Priority));
        Assert.IsFalse(resultHeader.MessageProperties.Persistent, nameof(resultHeader.MessageProperties.Persistent));
    }

    [TestMethod]
    public async Task PublishWithPersistent()
    {
        await _carrotClient.PublishAsync(_eventDto, messageProperties: new MessageProperties { Persistent = true });

        CarrotHeader resultHeader = _resultingMessage.Header;
        AssertGeneralPublishCarrotHeaderProperties(resultHeader);
        Assert.IsNull(resultHeader.InitialUserName, nameof(resultHeader.InitialUserName));
        Assert.IsNull(resultHeader.InitialServiceName, nameof(resultHeader.InitialServiceName));
        Assert.IsTrue(resultHeader.MessageProperties.PublisherConfirm, nameof(resultHeader.MessageProperties.PublisherConfirm));
        Assert.AreEqual(null, resultHeader.MessageProperties.Ttl, nameof(resultHeader.MessageProperties.Ttl));
        Assert.AreEqual(0, resultHeader.MessageProperties.Priority, nameof(resultHeader.MessageProperties.Priority));
        Assert.IsTrue(resultHeader.MessageProperties.Persistent, nameof(resultHeader.MessageProperties.Persistent));
    }

    [TestMethod]
    public async Task PublishWithCustomRoutingEvent()
    {
        var dto = new CustomRoutingKeyDto(11);
        var context = new Context(UserName);

        await _carrotClient.PublishAsync(dto, context);

        CarrotHeader resultHeader = _resultingMessage.Header;
        Assert.AreEqual(UserName, resultHeader.InitialUserName, nameof(resultHeader.InitialUserName));
        Assert.AreEqual(typeof(CustomRoutingKeyDto).FullName, resultHeader.CalledMethod, nameof(resultHeader.CalledMethod));
        Assert.AreEqual(CustomRoutingKeyDto.CustomExchange, resultHeader.Exchange, nameof(resultHeader.Exchange));
        Assert.AreEqual(CustomRoutingKeyDto.CustomRoutingKey, resultHeader.RoutingKey, nameof(resultHeader.RoutingKey));
        Assert.AreEqual(string.Empty, resultHeader.ReplyExchange, nameof(resultHeader.ReplyExchange));
        Assert.AreEqual(string.Empty, resultHeader.ReplyRoutingKey, nameof(resultHeader.ReplyRoutingKey));
        Assert.IsFalse(resultHeader.IncludeRequestPayloadInResponse, nameof(resultHeader.IncludeRequestPayloadInResponse));
    }

    [TestMethod]
    public async Task SendQuery()
    {
        const string replyExchange = "myExchange";
        const string replyRoutingKey = "myRoutingKey";
        var correlationId = Guid.NewGuid();
        var replyEndpoint = new ExchangeReplyEndPoint(replyExchange, replyRoutingKey);
        var context = new Context(UserName);

        await _carrotClient.SendAsync(_queryDto, replyEndpoint, context, correlationId: correlationId);

        CarrotHeader resultHeader = _resultingMessage.Header;
        Assert.AreEqual(UserName, resultHeader.InitialUserName, nameof(resultHeader.InitialUserName));
        Assert.AreEqual(typeof(TestDto).FullName, resultHeader.CalledMethod, nameof(resultHeader.CalledMethod));
        Assert.AreEqual(string.Empty, resultHeader.Exchange, nameof(resultHeader.Exchange));
        Assert.AreEqual(TestQueueEndPoint.TestQueueName, resultHeader.RoutingKey, nameof(resultHeader.RoutingKey));
        Assert.AreEqual(correlationId, resultHeader.CorrelationId, nameof(resultHeader.CorrelationId));
        Assert.AreEqual(replyExchange, resultHeader.ReplyExchange, nameof(resultHeader.ReplyExchange));
        Assert.AreEqual(replyRoutingKey, resultHeader.ReplyRoutingKey, nameof(resultHeader.ReplyRoutingKey));
        Assert.IsFalse(resultHeader.IncludeRequestPayloadInResponse, nameof(resultHeader.IncludeRequestPayloadInResponse));
    }

    [TestMethod]
    public async Task SendCommand()
    {
        const string replyQueue = "myQueue";
        var correlationId = Guid.NewGuid();
        var replyEndpoint = new QueueReplyEndPoint(replyQueue);
        var context = new Context(UserName);

        await _carrotClient.SendAsync(_commandDto, replyEndpoint, context, correlationId: correlationId);

        CarrotHeader resultHeader = _resultingMessage.Header;
        Assert.AreEqual(UserName, resultHeader.InitialUserName, nameof(resultHeader.InitialUserName));
        Assert.AreEqual(typeof(TestDto).FullName, resultHeader.CalledMethod, nameof(resultHeader.CalledMethod));
        Assert.AreEqual(string.Empty, resultHeader.Exchange, nameof(resultHeader.Exchange));
        Assert.AreEqual(TestQueueEndPoint.TestQueueName, resultHeader.RoutingKey, nameof(resultHeader.RoutingKey));
        Assert.AreEqual(correlationId, resultHeader.CorrelationId, nameof(resultHeader.CorrelationId));
        Assert.AreEqual(string.Empty, resultHeader.ReplyExchange, nameof(resultHeader.ReplyExchange));
        Assert.AreEqual(replyQueue, resultHeader.ReplyRoutingKey, nameof(resultHeader.ReplyRoutingKey));
        Assert.IsFalse(resultHeader.IncludeRequestPayloadInResponse, nameof(resultHeader.IncludeRequestPayloadInResponse));
    }

    [TestMethod]
    public async Task SendQuery_Without_CorrelationId()
    {
        const string replyQueue = "myQueue";
        var replyEndpoint = new QueueReplyEndPoint(replyQueue);
        var context = new Context(UserName);

        await _carrotClient.SendAsync(_queryDto, replyEndpoint, context);

        CarrotHeader resultHeader = _resultingMessage.Header;
        Assert.IsNull(resultHeader.CorrelationId, nameof(resultHeader.CorrelationId));
    }

    [TestMethod]
    public async Task SendCommand_Without_CorrelationId()
    {
        var context = new Context(UserName);

        await _carrotClient.SendAsync(_commandDto, context: context);

        CarrotHeader resultHeader = _resultingMessage.Header;
        Assert.IsNull(resultHeader.CorrelationId, nameof(resultHeader.CorrelationId));
    }

    [TestMethod]
    public async Task SendCommand_Without_Reply()
    {
        var context = new Context(UserName);

        await _carrotClient.SendAsync(_commandDto, context: context);

        CarrotHeader resultHeader = _resultingMessage.Header;
        Assert.AreEqual(string.Empty, resultHeader.ReplyExchange, nameof(resultHeader.ReplyExchange));
        Assert.AreEqual(string.Empty, resultHeader.ReplyRoutingKey, nameof(resultHeader.ReplyRoutingKey));
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public async Task IncludeRequestPayloadInResponse(bool includePayload)
    {
        const string replyExchange = "myExchange";
        const string replyRoutingKey = "myRoutingKey";
        var replyEndpoint = new ExchangeReplyEndPoint(replyExchange, replyRoutingKey, includePayload);

        await _carrotClient.SendAsync(_queryDto, replyEndpoint);

        CarrotHeader resultHeader = _resultingMessage.Header;
        Assert.AreEqual(includePayload, resultHeader.IncludeRequestPayloadInResponse, nameof(resultHeader.IncludeRequestPayloadInResponse));
    }

    [TestMethod]
    public async Task SendReceive_With_Default_Ttl()
    {
        var context = new Context(UserName);

        await _carrotClient.SendReceiveAsync(_commandDto, context);

        CarrotHeader resultHeader = _resultingMessage.Header;
        Assert.AreEqual(5_000, resultHeader.MessageProperties.Ttl, nameof(resultHeader.MessageProperties.Ttl));
    }

    [TestMethod]
    public async Task SendReceive_With_Default_Ttl_Without_Context()
    {
        await _carrotClient.SendReceiveAsync(_commandDto);

        CarrotHeader resultHeader = _resultingMessage.Header;
        Assert.AreEqual(5_000, resultHeader.MessageProperties.Ttl, nameof(resultHeader.MessageProperties.Ttl));
    }

    [DataRow(2_000)]
    [DataRow(0)]
    [TestMethod]
    public async Task SendReceive_With_Custom_Ttl(int ttl)
    {
        await _carrotClient.SendReceiveAsync(_commandDto, messageProperties: new MessageProperties { Ttl = ttl });

        CarrotHeader resultHeader = _resultingMessage.Header;
        Assert.AreEqual(ttl, resultHeader.MessageProperties.Ttl, nameof(resultHeader.MessageProperties.Ttl));
    }

    private static void AssertGeneralPublishCarrotHeaderProperties(CarrotHeader resultHeader)
    {
        Assert.AreEqual(typeof(TestDto).FullName, resultHeader.CalledMethod, nameof(resultHeader.CalledMethod));
        Assert.AreEqual(TestExchangeEndPoint.TestExchangeName, resultHeader.Exchange, nameof(resultHeader.Exchange));
        Assert.AreEqual(typeof(TestDto).FullName, resultHeader.RoutingKey, nameof(resultHeader.RoutingKey));
        Assert.AreEqual(string.Empty, resultHeader.ReplyExchange, nameof(resultHeader.ReplyExchange));
        Assert.AreEqual(string.Empty, resultHeader.ReplyRoutingKey, nameof(resultHeader.ReplyRoutingKey));
        Assert.IsFalse(resultHeader.IncludeRequestPayloadInResponse, nameof(resultHeader.IncludeRequestPayloadInResponse));
    }
}