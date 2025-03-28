using System.Text;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Serialization;
using CarrotMQ.RabbitMQ.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class ConsumerChannelTest
{
    private readonly TaskCompletionSource<bool> _consumerStarted = new(false);
    private readonly TaskCompletionSource<bool> _messageDistributorDelayTask = new(false);
    private readonly List<ulong> _rejectedMessages = [];
    private IBrokerConnection _brokerConnection = null!;

    private Func<CarrotMessage, Task<DeliveryStatus>> _consumeAsyncFunc = null!;
    private ConsumerChannel _consumerChannel = null!;

    private int _consumerTag = 1;

    [TestInitialize]
    public async Task Setup()
    {
        var connection = Substitute.For<IConnection>();
        var channel = Substitute.For<IChannel>();
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(channel));
        _consumerChannel = (ConsumerChannel)await ConsumerChannel.CreateAsync(
                connection,
                TimeSpan.FromSeconds(10),
                new ProtocolSerializer(),
                new BasicPropertiesMapper(),
                TestLoggerFactory.Instance)
            .ConfigureAwait(false);

        channel.When(c => c.BasicRejectAsync(Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()))
            .Do(info => { _rejectedMessages.Add(info.Arg<ulong>()); });

        _brokerConnection = Substitute.For<IBrokerConnection>();
        _brokerConnection.CreateConsumerChannelAsync().Returns(_consumerChannel);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task CarrotConsumer_graceful_shutdown_while_consuming()
    {
        _consumeAsyncFunc = async _ =>
        {
            _consumerStarted.SetResult(true);
            await _messageDistributorDelayTask.Task.ConfigureAwait(false);

            return DeliveryStatus.Ack;
        };

        await _consumerChannel.StartConsumingAsync("some.queue", 1, 0, _consumeAsyncFunc).ConfigureAwait(false);

        // Start processing of a new CarrotMessage (configured to be handled by our TestHandlerCaller)
        var consumeTask = ConsumeCarrotMessageAsync();
        await _consumerStarted.Task.ConfigureAwait(false);

        var stoppingTask = _consumerChannel.DisposeAsync();

        await Task.Delay(50).ConfigureAwait(false);

        // Let the message handling (the consume task) finish
        _messageDistributorDelayTask.SetResult(true);
        await consumeTask.ConfigureAwait(false);

        // Wait for our stop task to finish the shutdown
        await stoppingTask.ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task CarrotConsumer_consume_with_Exception()
    {
        _consumeAsyncFunc = async _ =>
        {
            _consumerStarted.SetResult(true);
            await _messageDistributorDelayTask.Task.ConfigureAwait(false);

            return DeliveryStatus.Ack;
        };

        await _consumerChannel.StartConsumingAsync("some.queue", 1, 0, _consumeAsyncFunc).ConfigureAwait(false);

        _messageDistributorDelayTask.SetException(new Exception("test"));

        await ConsumeCarrotMessageAsync().ConfigureAwait(false);

        Assert.AreEqual(1, _rejectedMessages.Count); // reject has been sent after 1s timeout despite the message not being finished processing
        Assert.AreEqual((ulong)1, _rejectedMessages.First()); // deliveryTag of rejected message = 1

        await _consumerChannel.DisposeAsync().ConfigureAwait(false);
    }

    private async Task ConsumeCarrotMessageAsync()
    {
        var serializer = new ProtocolSerializer();
        var carrotMessage = new CarrotMessage(new CarrotHeader { MessageId = Guid.NewGuid() }, string.Empty);
        ReadOnlyMemory<byte> bytePayload = Encoding.UTF8.GetBytes(serializer.Serialize(carrotMessage));

        await _consumerChannel.SendConsumerMessageReceivedEventAsync(
                new BasicDeliverEventArgs($"{_consumerTag++}", 1, false, "", "", new BasicProperties(), bytePayload))
            .ConfigureAwait(false);
    }
}