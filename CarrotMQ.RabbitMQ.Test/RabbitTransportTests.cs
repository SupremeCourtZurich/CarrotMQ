using CarrotMQ.Core;
using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Connectivity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class RabbitTransportTests
{
    public enum ConnectionState
    {
        None,
        Down,
        Open
    }

    private IBrokerConnection _brokerConnection = null!;
    private RabbitTransport _transport = null!;

    [TestInitialize]
    public void Initialize()
    {
        _brokerConnection = Substitute.For<IBrokerConnection>();
        _transport = new RabbitTransport(
            _brokerConnection,
            Options.Create(new CarrotTracingOptions()),
            NullLogger<RabbitTransport>.Instance);
    }

    [TestMethod]
    [Timeout(10_000)]
    [DataRow(ConnectionState.None, true, PublishFailureReason.Unreachable)]
    [DataRow(ConnectionState.Down, true, PublishFailureReason.Unreachable)]
    [DataRow(ConnectionState.Open, true, PublishFailureReason.Unknown)]
    [DataRow(ConnectionState.None, false, PublishFailureReason.Unreachable)]
    public async Task Send_ChannelNotAvailable_TokenCancelled_FailsWithTheConnectionsState(
        ConnectionState state,
        bool publisherConfirm,
        PublishFailureReason reason)
    {
        // Arrange
        UseConnection(state);
        ChannelsNeverBecomeAvailable();
        using var cts = new CancellationTokenSource();
        var send = _transport.SendAsync(CreateMessage(publisherConfirm), cts.Token);

        // Act
        cts.Cancel();

        // Assert
        await AssertCancelledAsync(send, reason).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task SendReceive_ChannelNotAvailable_TokenCancelled_FailsAsUnreachable()
    {
        // Arrange
        UseConnection(ConnectionState.None);
        ChannelsNeverBecomeAvailable();
        using var cts = new CancellationTokenSource();
        var send = _transport.SendReceiveAsync(CreateMessage(true), cts.Token);

        // Act
        cts.Cancel();

        // Assert
        await AssertCancelledAsync(send, PublishFailureReason.Unreachable).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task Send_BrokerUnreachableWhenConnecting_FailsAsUnreachable()
    {
        // Arrange
        var unreachable = new BrokerUnreachableException(new InvalidOperationException("broker down"));
        _brokerConnection.GetPublisherChannelWithConfirmsAsync().Returns<IPublisherChannel>(_ => throw unreachable);

        // Act
        var send = _transport.SendAsync(CreateMessage(true), CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsExactlyAsync<PublishFailedException>(() => send).ConfigureAwait(false);
        Assert.AreEqual(PublishFailureReason.Unreachable, exception.Reason);
        Assert.AreSame(unreachable, exception.InnerException);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task SendReceive_BrokerUnreachableWhenConnecting_FailsAsUnreachable()
    {
        // Arrange
        var unreachable = new BrokerUnreachableException(new InvalidOperationException("broker down"));
        _brokerConnection.GetDirectReplyConfirmChannelAsync().Returns<IDirectReplyChannel>(_ => throw unreachable);

        // Act
        var send = _transport.SendReceiveAsync(CreateMessage(true), CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsExactlyAsync<PublishFailedException>(() => send).ConfigureAwait(false);
        Assert.AreEqual(PublishFailureReason.Unreachable, exception.Reason);
        Assert.AreSame(unreachable, exception.InnerException);
    }

    private static async Task AssertCancelledAsync(Task send, PublishFailureReason reason)
    {
        await WaitUntilAsync(() => send.IsCompleted, "the cancelled send completes while no channel is available").ConfigureAwait(false);
        var exception = await Assert.ThrowsExactlyAsync<PublishFailedException>(() => send).ConfigureAwait(false);
        Assert.AreEqual(reason, exception.Reason);
        Assert.IsInstanceOfType<OperationCanceledException>(exception.InnerException);
    }

    private static CarrotMessage CreateMessage(bool publisherConfirm)
    {
        var header = new CarrotHeader
        {
            CorrelationId = Guid.NewGuid(),
            MessageProperties = MessageProperties.Default with { PublisherConfirm = publisherConfirm }
        };

        return new CarrotMessage(header, string.Empty);
    }

    private void ChannelsNeverBecomeAvailable()
    {
        _brokerConnection.GetPublisherChannelAsync().Returns(new TaskCompletionSource<IPublisherChannel>().Task);
        _brokerConnection.GetPublisherChannelWithConfirmsAsync().Returns(new TaskCompletionSource<IPublisherChannel>().Task);
        _brokerConnection.GetDirectReplyChannelAsync().Returns(new TaskCompletionSource<IDirectReplyChannel>().Task);
        _brokerConnection.GetDirectReplyConfirmChannelAsync().Returns(new TaskCompletionSource<IDirectReplyChannel>().Task);
    }

    private void UseConnection(ConnectionState state)
    {
        if (state == ConnectionState.None)
        {
            _brokerConnection.CurrentConnection.Returns((IConnection?)null);

            return;
        }

        var connection = Substitute.For<IConnection>();
        connection.IsOpen.Returns(state == ConnectionState.Open);
        _brokerConnection.CurrentConnection.Returns(connection);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(4);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"Not within 4 s: {description}");

            await Task.Delay(10).ConfigureAwait(false);
        }
    }
}
