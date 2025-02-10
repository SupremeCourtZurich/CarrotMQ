using System.Text;
using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Telemetry;
using CarrotMQ.RabbitMQ.Configuration.Queues;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Serialization;
using CarrotMQ.RabbitMQ.Test.Helper;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class CarrotConsumerTests
{
    private readonly TaskCompletionSource<bool> _consumerStarted = new(false);
    private readonly TaskCompletionSource<bool> _messageDistributorDelayTask = new(false);
    private readonly List<ulong> _rejectedMessages = [];
    private IBrokerConnection _brokerConnection = null!;

    private Func<BasicDeliverEventArgs, Task> _consumeAsyncFunc = null!;
    private IMessageDistributor _messageDistributor = null!;
    private IProtocolSerializer _protocolSerializer = null!;

    private ulong _sentMessageCounter;

    [TestInitialize]
    public void Setup()
    {
        _protocolSerializer = new ProtocolSerializer();
        _messageDistributor = Substitute.For<IMessageDistributor>();

        var consumerChannel = Substitute.For<IConsumerChannel>();
        consumerChannel.ApplyConfigurations(Arg.Any<QueueConfiguration>(), Arg.Any<IList<BindingConfiguration>>()).Returns(Task.CompletedTask);
        consumerChannel.When(
                c => c.StartConsumingAsync(
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Any<ushort>(),
                    Arg.Any<Func<BasicDeliverEventArgs, Task>>(),
                    Arg.Any<IDictionary<string, object?>?>()))
            .Do(info => { _consumeAsyncFunc = info.Arg<Func<BasicDeliverEventArgs, Task>>(); });
        consumerChannel.When(c => c.RejectAsync(Arg.Any<ulong>(), Arg.Any<bool>()))
            .Do(info => { _rejectedMessages.Add(info.Arg<ulong>()); });
        _brokerConnection = Substitute.For<IBrokerConnection>();
        _brokerConnection.CreateConsumerChannelAsync().Returns(consumerChannel);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task CarrotConsumer_graceful_shutdown_while_consuming()
    {
        _messageDistributor.DistributeAsync(Arg.Any<CarrotMessage>(), Arg.Any<CancellationToken>())
            .Returns(
                async _ =>
                {
                    _consumerStarted.SetResult(true);
                    await _messageDistributorDelayTask.Task.ConfigureAwait(false);

                    return DeliveryStatus.Ack;
                });
        var consumer = CreateConsumer();

        // Start consumer
        await consumer.InitializeAsync().ConfigureAwait(false);

        // Start processing of a new CarrotMessage (configured to be handled by our TestHandlerCaller)
        var consumeTask = ConsumeCarrotMessageAsync();
        await _consumerStarted.Task.ConfigureAwait(false);

        var stoppingTask = consumer.DisposeAsync();

        await Task.Delay(50).ConfigureAwait(false);

        // Let the message handling (the consume task) finish
        _messageDistributorDelayTask.SetResult(true);
        await consumeTask.ConfigureAwait(false);

        // Wait for our stop task to finish the shutdown
        await stoppingTask.ConfigureAwait(false);

        await consumer.DisposeAsync().ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task CarrotConsumer_graceful_shutdown_with_MessageProcessingTimeout()
    {
        _messageDistributor.DistributeAsync(Arg.Any<CarrotMessage>(), Arg.Any<CancellationToken>())
            .Returns(
                async x =>
                {
                    _consumerStarted.SetResult(true);
                    var token = x.Arg<CancellationToken>();

                    while (token.IsCancellationRequested == false)
                    {
                        await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
                    }

                    return DeliveryStatus.Ack;
                });
        var consumer = CreateConsumer(50);

        // Start consumer
        await consumer.InitializeAsync().ConfigureAwait(false);

        // Start processing of a new CarrotMessage (configured to be handled by our TestHandlerCaller)
        var consumeTask = ConsumeCarrotMessageAsync();
        await _consumerStarted.Task.ConfigureAwait(false);

        var stoppingTask = consumer.DisposeAsync();

        await Task.Delay(50).ConfigureAwait(false);

        // Let the message handling (the consume task) finish
        await consumeTask.ConfigureAwait(false);

        // Wait for our stop task to finish the shutdown
        await stoppingTask.ConfigureAwait(false);

        await consumer.DisposeAsync().ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task CarrotConsumer_consume_with_Exception()
    {
        _messageDistributor.DistributeAsync(Arg.Any<CarrotMessage>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new Exception("test"));
        var consumer = CreateConsumer();

        await consumer.InitializeAsync().ConfigureAwait(false);

        _messageDistributorDelayTask.SetException(new Exception("test"));

        await ConsumeCarrotMessageAsync().ConfigureAwait(false);

        Assert.AreEqual(1, _rejectedMessages.Count); // reject has been sent after 1s timeout despite the message not being finished processing
        Assert.AreEqual((ulong)1, _rejectedMessages.First()); // deliveryTag of rejected message = 1

        await consumer.DisposeAsync().ConfigureAwait(false);
    }

    private CarrotConsumer CreateConsumer(int? messageProcessingTimeout = null)
    {
        var consumerConfig = new ConsumerConfiguration { AckCount = 1 };
        if (messageProcessingTimeout != null) consumerConfig.MessageProcessingTimeout = TimeSpan.FromMilliseconds(messageProcessingTimeout.Value);

        var config = new QueueConfiguration
        {
            QueueName = "no-queue",
            ConsumerConfiguration = consumerConfig
        };

        return new CarrotConsumer(
            config,
            [],
            _messageDistributor,
            _brokerConnection,
            new ProtocolSerializer(),
            TestLoggerFactory.CreateLogger<CarrotConsumer>(),
            Substitute.For<ICarrotMetricsRecorder>(),
            Options.Create(new CarrotTracingOptions()));
    }

    private async Task ConsumeCarrotMessageAsync()
    {
        var carrotMessage = new CarrotMessage(new CarrotHeader { MessageId = Guid.NewGuid() }, string.Empty);
        _sentMessageCounter++;
        var basicProperties = Substitute.For<IBasicProperties>();
        basicProperties.MessageId.Returns(Guid.NewGuid().ToString());
        var payload = _protocolSerializer.Serialize(carrotMessage);
        BasicDeliverEventArgs eventArgs = new(
            "consumerTag",
            _sentMessageCounter,
            false,
            "exchange",
            "routingKey",
            basicProperties,
            Encoding.UTF8.GetBytes(payload));

        await _consumeAsyncFunc(eventArgs).ConfigureAwait(false);
    }
}