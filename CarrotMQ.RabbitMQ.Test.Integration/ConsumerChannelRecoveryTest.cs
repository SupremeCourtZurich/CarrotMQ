using System.Text;
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
        var brokerConnection = TestBase.ProducerHost.Host.Services.GetRequiredService<IBrokerConnection>();
        var loggerFactory = TestBase.ProducerHost.Host.Services.GetRequiredService<ILoggerFactory>();

        var connection = await brokerConnection.ConnectAsync().ConfigureAwait(false);
        var consumer = await ConsumerChannel.CreateAsync(connection, brokerConnection.NetworkRecoveryInterval, loggerFactory).ConfigureAwait(false);
        await using var _1 = consumer.ConfigureAwait(false);

        var publisher = await PublisherConfirmChannel.CreateAsync(
                connection,
                brokerConnection.NetworkRecoveryInterval,
                new PublisherConfirmOptions(),
                new BasicPropertiesMapper(),
                loggerFactory)
            .ConfigureAwait(false);

        await using var _ = publisher.ConfigureAwait(false);

        await consumer.DeclareQueueAsync(QueueName, true, false, false).ConfigureAwait(false);
        await consumer.StartConsumingAsync(
                QueueName,
                false,
                0,
                async args =>
                {
                    var payload = Encoding.UTF8.GetString(args.Body.ToArray());
                    await msgQueue.Writer.WriteAsync(int.Parse(payload)).ConfigureAwait(false);
                })
            .ConfigureAwait(false);

        using var cts = new CancellationTokenSource(10_000);

        await publisher.PublishAsync("1", new CarrotHeader { RoutingKey = QueueName }, cts.Token).ConfigureAwait(false);
        var receivedId = await msgQueue.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
        Assert.AreEqual(1, receivedId);

        await publisher.PublishAsync("2", new CarrotHeader { RoutingKey = QueueName }, cts.Token).ConfigureAwait(false);
        receivedId = await msgQueue.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
        Assert.AreEqual(2, receivedId);

        await consumer.AckAsync(999, false).ConfigureAwait(false); // Force channel interruption

        receivedId = await msgQueue.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
        Assert.AreEqual(1, receivedId);
        receivedId = await msgQueue.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
        Assert.AreEqual(2, receivedId);

        await publisher.PublishAsync("3", new CarrotHeader { RoutingKey = QueueName }, cts.Token).ConfigureAwait(false);
        receivedId = await msgQueue.Reader.ReadAsync(cts.Token).ConfigureAwait(false);
        Assert.AreEqual(3, receivedId);
    }
}