using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Telemetry;
using CarrotMQ.RabbitMQ.Configuration.Queues;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Test.Helper;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class CarrotConsumerTests
{
    private readonly TaskCompletionSource<bool> _consumerStarted = new(false);
    private readonly TaskCompletionSource<bool> _messageDistributorDelayTask = new(false);
    private IBrokerConnection _brokerConnection = null!;

    private Func<CarrotMessage, Task<DeliveryStatus>> _consumeAsyncFunc = null!;
    private IMessageDistributor _messageDistributor = null!;

    [TestInitialize]
    public void Setup()
    {
        _messageDistributor = Substitute.For<IMessageDistributor>();

        var consumerChannel = Substitute.For<IConsumerChannel>();
        consumerChannel.ApplyConfigurations(Arg.Any<QueueConfiguration>(), Arg.Any<IList<BindingConfiguration>>()).Returns(Task.CompletedTask);
        consumerChannel.When(
                c => c.StartConsumingAsync(
                    Arg.Any<string>(),
                    Arg.Any<ushort>(),
                    Arg.Any<ushort>(),
                    Arg.Any<Func<CarrotMessage, Task<DeliveryStatus>>>(),
                    Arg.Any<IDictionary<string, object?>?>()))
            .Do(info => { _consumeAsyncFunc = info.Arg<Func<CarrotMessage, Task<DeliveryStatus>>>(); });
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
        var deliveryStatus = await consumeTask.ConfigureAwait(false);

        // Wait for our stop task to finish the shutdown
        await stoppingTask.ConfigureAwait(false);

        await consumer.DisposeAsync().ConfigureAwait(false);

        Assert.AreEqual(DeliveryStatus.Ack, deliveryStatus);
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
        var deliveryStatus = await consumeTask.ConfigureAwait(false);

        // Wait for our stop task to finish the shutdown
        await stoppingTask.ConfigureAwait(false);

        await consumer.DisposeAsync().ConfigureAwait(false);

        Assert.AreEqual(DeliveryStatus.Ack, deliveryStatus);
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

        var deliveryStatus = await ConsumeCarrotMessageAsync().ConfigureAwait(false);

        Assert.AreEqual(DeliveryStatus.Reject, deliveryStatus);

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
            TestLoggerFactory.CreateLogger<CarrotConsumer>(),
            Substitute.For<ICarrotMetricsRecorder>(),
            Options.Create(new CarrotTracingOptions()));
    }

    private async Task<DeliveryStatus> ConsumeCarrotMessageAsync()
    {
        var carrotMessage = new CarrotMessage(new CarrotHeader { MessageId = Guid.NewGuid() }, string.Empty);

        return await _consumeAsyncFunc(carrotMessage).ConfigureAwait(false);
    }
}