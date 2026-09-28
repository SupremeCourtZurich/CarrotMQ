using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace CarrotMQ.RabbitMQ.Test.Integration;

[TestClass]
public sealed class BrokerConnectionTest
{
    private IBrokerConnection _brokerConnection = null!;

    private CarrotHelper _carrotHelper = null!;

    [TestInitialize]
    public void Initialize()
    {
        _carrotHelper = new CarrotHelper(
            $"{nameof(BrokerConnectionTest)}",
            _ => { },
            _ => { });

        _brokerConnection = _carrotHelper.Host.Services.GetRequiredService<IBrokerConnection>();
    }

    [TestMethod]
    [Timeout(30_000)]
    [DataRow(true)]
    [DataRow(false)]
    public async Task BrokerConnection_Close_Connect(bool implicitConnect)
    {
        var publisherChannel = await _brokerConnection.GetPublisherChannelAsync().ConfigureAwait(false);
        var publisherConfirmChannel = await _brokerConnection.GetPublisherChannelWithConfirmsAsync().ConfigureAwait(false);
        var directReplyChannel = await _brokerConnection.GetDirectReplyChannelAsync().ConfigureAwait(false);
        var directReplyWithConfirmChannel = await _brokerConnection.GetDirectReplyConfirmChannelAsync().ConfigureAwait(false);
        var consumerChannel = await _brokerConnection.CreateConsumerChannelAsync().ConfigureAwait(false);

        bool connectionClosingWasFired = false;
        _brokerConnection.ConnectionClosing += (_, _) =>
        {
            connectionClosingWasFired = true;

            return Task.CompletedTask;
        };

        await _brokerConnection.CloseAsync().ConfigureAwait(false);

        Assert.IsTrue(connectionClosingWasFired, $"{nameof(_brokerConnection.ConnectionClosing)} was not fired");
        Assert.IsTrue(publisherChannel.IsClosed, "Publisher channel should be closed");
        Assert.IsTrue(publisherConfirmChannel.IsClosed, "Publisher confirm channel should be closed");
        Assert.IsTrue(directReplyChannel.IsClosed, "Direct reply channel should be closed");
        Assert.IsTrue(directReplyWithConfirmChannel.IsClosed, "Direct reply confirm channel should be closed");
        Assert.IsTrue(consumerChannel.IsClosed, "Consumer channel should be closed");

        if (!implicitConnect)
        {
            await _brokerConnection.ConnectAsync().ConfigureAwait(false);
        }

        var publisherChannel2 = await _brokerConnection.GetPublisherChannelAsync().ConfigureAwait(false);
        var publisherConfirmChannel2 = await _brokerConnection.GetPublisherChannelWithConfirmsAsync().ConfigureAwait(false);
        var directReplyChannel2 = await _brokerConnection.GetDirectReplyChannelAsync().ConfigureAwait(false);
        var directReplyWithConfirmChannel2 = await _brokerConnection.GetDirectReplyConfirmChannelAsync().ConfigureAwait(false);
        var consumerChannel2 = await _brokerConnection.CreateConsumerChannelAsync().ConfigureAwait(false);

        Assert.IsTrue(publisherChannel2.IsOpen, "Publisher channel should be open");
        Assert.IsTrue(publisherConfirmChannel2.IsOpen, "Publisher confirm channel should be open");
        Assert.IsTrue(directReplyChannel2.IsOpen, "Direct reply channel should be open");
        Assert.IsTrue(directReplyWithConfirmChannel2.IsOpen, "Direct reply confirm channel should be open");
        Assert.IsTrue(consumerChannel2.IsOpen, "Consumer channel should be open");
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task CurrentConnection_Tracks_Connect_And_Close()
    {
        // Let the hosted service finish its own connect first, so it cannot reconnect behind the close below.
        await _carrotHelper.WaitForConsumerHostBootstrapToCompleteAsync().ConfigureAwait(false);

        var connection = await _brokerConnection.ConnectAsync().ConfigureAwait(false);

        Assert.AreSame(connection, _brokerConnection.CurrentConnection);
        Assert.IsTrue(_brokerConnection.CurrentConnection!.IsOpen, "Current connection should be open");

        await _brokerConnection.CloseAsync().ConfigureAwait(false);

        Assert.IsNull(_brokerConnection.CurrentConnection, "No connection expected after close");
    }

    [TestCleanup]
    public void Cleanup()
    {
        _carrotHelper.Dispose();
    }
}