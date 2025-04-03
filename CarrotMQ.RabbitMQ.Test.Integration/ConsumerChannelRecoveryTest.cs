using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Serialization;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Channel = System.Threading.Channels.Channel;

namespace CarrotMQ.RabbitMQ.Test.Integration;

[TestClass]
[TestCategory("Integration")]
public class ConsumerChannelRecoveryTestTestBase
{
    private const string QueueName = "test.consumer.recovery.queue";

    [TestMethod]
    public async Task ConsumerChannel_recovery_after_channel_interruption()
    {
        var msgQueue = Channel.CreateBounded<int>(10);
        var brokerConnection = TestBase.ConsumerHost.Host.Services.GetRequiredService<IBrokerConnection>();
        var loggerFactory = TestBase.ConsumerHost.Host.Services.GetRequiredService<ILoggerFactory>();
        var protocolSerializer = new ProtocolSerializer();
        var connection = await brokerConnection.ConnectAsync().ConfigureAwait(false);
        var consumer = await ConsumerChannel.CreateAsync(
                connection,
                brokerConnection.NetworkRecoveryInterval,
                protocolSerializer,
                loggerFactory)
            .ConfigureAwait(false);
        await using var _1 = consumer.ConfigureAwait(false);

        var publisher = await PublisherConfirmChannel.CreateAsync(
                connection,
                brokerConnection.NetworkRecoveryInterval,
                new PublisherConfirmOptions(),
                protocolSerializer,
                loggerFactory)
            .ConfigureAwait(false);

        await using var _ = publisher.ConfigureAwait(false);

        await consumer.DeclareQueueAsync(QueueName, true, false, false).ConfigureAwait(false);

        var tcsMessage1 = new TaskCompletionSource<bool>();
        var tcsMessage2 = new TaskCompletionSource<bool>();
        var blockMesageConsumer = true;
        await consumer.StartConsumingAsync(
                QueueName,
                1,
                0,
                async message =>
                {
                    Console.WriteLine(message.Payload + " received");
                    await msgQueue.Writer.WriteAsync(int.Parse(message.Payload ?? "0")).ConfigureAwait(false);
                    if ("1".Equals(message.Payload) && blockMesageConsumer)
                    {
                        await tcsMessage1.Task.ConfigureAwait(false);
                    }
                    else if ("2".Equals(message.Payload) && blockMesageConsumer)
                    {
                        await tcsMessage2.Task.ConfigureAwait(false);
                    }

                    return DeliveryStatus.Ack;
                })
            .ConfigureAwait(false);

        consumer.UnregisteredAsync += (_, _) =>
        {
            // Release the "old" message consumer -> if not, the new consumer channel creation is blocked
            blockMesageConsumer = false;
            tcsMessage1.SetResult(true);
            tcsMessage2.SetResult(true);

            return Task.CompletedTask;
        };
        using var cts = new CancellationTokenSource(10_000);

        await publisher.PublishAsync(new CarrotMessage(new CarrotHeader { RoutingKey = QueueName }, "1"), cts.Token).ConfigureAwait(false);
        var receivedId = await msgQueue.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
        Assert.AreEqual(1, receivedId);

        await publisher.PublishAsync(new CarrotMessage(new CarrotHeader { RoutingKey = QueueName }, "2"), cts.Token).ConfigureAwait(false);
        receivedId = await msgQueue.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
        Assert.AreEqual(2, receivedId);

        await consumer.AckAsync(999, false).ConfigureAwait(false); // Force channel interruption

        var receivedIds = new List<int>();
        receivedId = await msgQueue.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
        receivedIds.Add(receivedId);
        receivedId = await msgQueue.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
        receivedIds.Add(receivedId);

        Assert.IsTrue(receivedIds.Contains(1));
        Assert.IsTrue(receivedIds.Contains(2));

        await publisher.PublishAsync(new CarrotMessage(new CarrotHeader { RoutingKey = QueueName }, "3"), cts.Token).ConfigureAwait(false);
        receivedId = await msgQueue.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
        Assert.AreEqual(3, receivedId);
    }
}