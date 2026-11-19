using CarrotMQ.Core;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Serialization;
using CarrotMQ.RabbitMQ.Test.Helper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using IChannel = RabbitMQ.Client.IChannel;
using ShutdownAsyncEventHandler = RabbitMQ.Client.Events.AsyncEventHandler<RabbitMQ.Client.Events.ShutdownEventArgs>;

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class PublisherChannelTests
{
    private BasicProperties _basicProperties = null!;
    private IPublisherChannel _channel = null!;
    private IConnection _connection = null!;
    private IChannel _rabbitChannel = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        var channel = Substitute.For<IChannel>();
        _rabbitChannel = channel;
        channel.IsOpen.Returns(true);

        channel.When(m => m.BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>()))
            .Do(a => { _basicProperties = a.Arg<BasicProperties>(); });

        var connection = Substitute.For<IConnection>();
        _connection = connection;
        connection.IsOpen.Returns(true);
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>())
            .Returns(_ => Task.FromResult(channel));

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Trace)
                .AddConsole();
        });
        _channel = await PublisherChannel.CreateAsync(
                connection,
                TimeSpan.FromSeconds(2),
                new ProtocolSerializer(),
                loggerFactory)
            .ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task BrokerUnreachableWhileRecovering_TokenCancelled_FailsAsUnreachable()
    {
        // Arrange
        await MakeBrokerUnreachableAsync().ConfigureAwait(false);
        using var cts = new CancellationTokenSource();
        var publish = _channel.PublishAsync(new CarrotMessage(new CarrotHeader(), string.Empty), cts.Token);

        try
        {
            // Act
            cts.Cancel();

            // Assert
            await Wait.UntilAsync(() => publish.IsCompleted, "the cancelled publish completes while the broker is unreachable")
                .ConfigureAwait(false);
            await AssertCancelledAsync(publish, PublishFailureReason.Unreachable).ConfigureAwait(false);
        }
        finally
        {
            MakeBrokerReachable();
        }
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task ConnectionBackWhileTheChannelIsStillRecovering_TokenCancelled_FailsAsUnreachable()
    {
        // Arrange
        await MakeBrokerUnreachableAsync().ConfigureAwait(false);
        _connection.IsOpen.Returns(true);
        using var cts = new CancellationTokenSource();
        var publish = _channel.PublishAsync(new CarrotMessage(new CarrotHeader(), string.Empty), cts.Token);

        try
        {
            // Act
            cts.Cancel();

            // Assert
            await Wait.UntilAsync(() => publish.IsCompleted, "the cancelled publish completes while the channel is reopened")
                .ConfigureAwait(false);
            await AssertCancelledAsync(publish, PublishFailureReason.Unreachable).ConfigureAwait(false);
        }
        finally
        {
            MakeBrokerReachable();
        }
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ConnectionOpen_TokenAlreadyCancelled_FailsWithAnUnknownReason()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var publish = _channel.PublishAsync(new CarrotMessage(new CarrotHeader(), string.Empty), cts.Token);

        // Assert
        await AssertCancelledAsync(publish, PublishFailureReason.Unknown).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ConnectionForcedClosed_PublishOnTheClosedChannel_FailsAsUnreachable()
    {
        // Arrange
        _connection.IsOpen.Returns(false);
        ChannelAlreadyClosedBy(new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED"));

        // Act
        var publish = _channel.PublishAsync(new CarrotMessage(new CarrotHeader(), string.Empty), CancellationToken.None);

        // Assert
        await AssertClosedAsync(publish, PublishFailureReason.Unreachable).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ChannelClosedByAnotherPublishsRefusal_PublishOnTheClosedChannel_FailsWithAnUnknownReason()
    {
        // Arrange
        ChannelAlreadyClosedBy(new ShutdownEventArgs(ShutdownInitiator.Peer, 404, "NOT_FOUND", 60, 40));

        // Act
        var publish = _channel.PublishAsync(new CarrotMessage(new CarrotHeader(), string.Empty), CancellationToken.None);

        // Assert
        await AssertClosedAsync(publish, PublishFailureReason.Unknown).ConfigureAwait(false);
    }

    // The client may race a publish against the close it is handling, so CarrotMQ must not hand it one.
    [TestMethod]
    [Timeout(5_000)]
    public async Task ChannelShutdownReceived_PublishBeforeItReopens_IsNotHandedToTheClosedChannel()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        ClientClosesTheChannel();

        // Act
        var publish = _channel.PublishAsync(new CarrotMessage(new CarrotHeader(), string.Empty), cts.Token);

        // Assert
        await VerifyBasicPublishAsync(0).ConfigureAwait(false);
        cts.Cancel();
        _ = publish.Exception;
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ChannelClosed_PublishWaits_IsSentOnceTheClientReopensTheChannel()
    {
        // Arrange
        ClientClosesTheChannel();
        var publish = _channel.PublishAsync(new CarrotMessage(new CarrotHeader(), string.Empty), CancellationToken.None);
        var sentWhileClosed = CountBasicPublishes();

        // Act
        ClientReopensTheChannel();

        // Assert
        await Wait.UntilAsync(() => publish.IsCompleted, "the waiting publish completes after the reopen").ConfigureAwait(false);
        await publish.ConfigureAwait(false);
        Assert.AreEqual(0, sentWhileClosed);
        await VerifyBasicPublishAsync(1).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ChannelClosed_TokenCancelledWhileWaitingForTheReopen_FailsAsUnreachable()
    {
        // Arrange
        ClientClosesTheChannel();
        using var cts = new CancellationTokenSource();
        var publish = _channel.PublishAsync(new CarrotMessage(new CarrotHeader(), string.Empty), cts.Token);

        // Act
        cts.Cancel();

        // Assert
        await Wait.UntilAsync(() => publish.IsCompleted, "the cancelled publish completes").ConfigureAwait(false);
        await AssertCancelledAsync(publish, PublishFailureReason.Unreachable).ConfigureAwait(false);
        await VerifyBasicPublishAsync(0).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ChannelClosed_DisposedWhileWaitingForTheReopen_PublishFails()
    {
        // Arrange
        ClientClosesTheChannel();
        var publish = _channel.PublishAsync(new CarrotMessage(new CarrotHeader(), string.Empty), CancellationToken.None);

        // Act
        await _channel.DisposeAsync().ConfigureAwait(false);

        // Assert
        await Wait.UntilAsync(() => publish.IsCompleted, "the waiting publish completes once the channel is disposed").ConfigureAwait(false);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => publish).ConfigureAwait(false);
        await VerifyBasicPublishAsync(0).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ChannelCannotBeCreated_DoesNotSubscribeToTheConnection()
    {
        // Arrange
        var connection = Substitute.For<IConnection>();
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>())
            .Returns<Task<IChannel>>(_ => throw new BrokerUnreachableException(new InvalidOperationException("down")));

        // Act
        await Assert.ThrowsExactlyAsync<BrokerUnreachableException>(
                () => PublisherChannel.CreateAsync(connection, TimeSpan.FromSeconds(2), new ProtocolSerializer(), NullLoggerFactory.Instance))
            .ConfigureAwait(false);

        // Assert
        connection.DidNotReceive().RecoverySucceededAsync += Arg.Any<AsyncEventHandler<AsyncEventArgs>>();
    }

    // A close below 400 is left to the client's own recovery, which reopens the same channel.
    private void ClientClosesTheChannel()
    {
        _rabbitChannel.IsOpen.Returns(false);
        _rabbitChannel.ChannelShutdownAsync += Raise.Event<ShutdownAsyncEventHandler>(
            _rabbitChannel,
            new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED"));
    }

    private void ClientReopensTheChannel()
    {
        _rabbitChannel.IsOpen.Returns(true);
        _connection.RecoverySucceededAsync += Raise.Event<AsyncEventHandler<AsyncEventArgs>>(_connection, AsyncEventArgs.Empty);
    }

    private int CountBasicPublishes()
    {
        return _rabbitChannel.ReceivedCalls()
            .Count(c => string.Equals(c.GetMethodInfo().Name, nameof(IChannel.BasicPublishAsync), StringComparison.Ordinal));
    }

    private Task VerifyBasicPublishAsync(int times)
    {
        return _rabbitChannel.Received(times)
            .BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
            .AsTask();
    }

    // The client still reports the channel open, then rejects the publish: the close arrived during it.
    private void ChannelAlreadyClosedBy(ShutdownEventArgs close)
    {
        _rabbitChannel.When(m => m.BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>()))
            .Do(_ => throw new AlreadyClosedException(close));
    }

    private static async Task AssertClosedAsync(Task publish, PublishFailureReason reason)
    {
        var exception = await Assert.ThrowsExactlyAsync<PublishFailedException>(() => publish).ConfigureAwait(false);
        Assert.AreEqual(reason, exception.Reason, exception.Message);
        Assert.IsInstanceOfType<AlreadyClosedException>(exception.InnerException);
    }

    private static async Task AssertCancelledAsync(Task publish, PublishFailureReason reason)
    {
        var exception = await Assert.ThrowsExactlyAsync<PublishFailedException>(() => publish).ConfigureAwait(false);
        Assert.AreEqual(reason, exception.Reason);
        Assert.IsInstanceOfType<OperationCanceledException>(exception.InnerException);
    }

    // Closes the channel like a lost connection; the recovery loop keeps failing to reopen it and holds ChannelLock.
    private async Task MakeBrokerUnreachableAsync()
    {
        _connection.IsOpen.Returns(false);
        _rabbitChannel.IsOpen.Returns(false);
        _connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>(), Arg.Any<CancellationToken>())
            .Returns<IChannel>(_ => throw new BrokerUnreachableException(new InvalidOperationException("broker down")));
        var opensBefore = CountChannelOpens();
        _rabbitChannel.ChannelShutdownAsync += Raise.Event<ShutdownAsyncEventHandler>(null, new ShutdownEventArgs(ShutdownInitiator.Library, 541, "Unexpected"));
        await Wait.UntilAsync(() => CountChannelOpens() > opensBefore, "the recovery loop tries to reopen the channel").ConfigureAwait(false);
    }

    private void MakeBrokerReachable()
    {
        _connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(_rabbitChannel));
        _rabbitChannel.IsOpen.Returns(true);
        _connection.IsOpen.Returns(true);
    }

    private int CountChannelOpens()
    {
        return _connection.ReceivedCalls()
            .Count(c => string.Equals(c.GetMethodInfo().Name, nameof(IConnection.CreateChannelAsync), StringComparison.Ordinal));
    }


    [TestMethod]
    public async Task BasicProperties_ContentType()
    {
        var header = new CarrotHeader();
        await _channel.PublishAsync(new CarrotMessage(header, string.Empty), CancellationToken.None);

        Assert.AreEqual("application/json", _basicProperties.ContentType);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task BasicProperties_Persistent(bool persistent)
    {
        var header = new CarrotHeader { MessageProperties = new MessageProperties { Persistent = persistent } };
        await _channel.PublishAsync(new CarrotMessage(header, string.Empty), CancellationToken.None);

        Assert.AreEqual(persistent, _basicProperties.Persistent);
    }

    [TestMethod]
    [DataRow((byte)0)]
    [DataRow((byte)1)]
    [DataRow((byte)9)]
    public async Task BasicProperties_Priority(byte priority)
    {
        var header = new CarrotHeader { MessageProperties = new MessageProperties { Priority = priority } };
        await _channel.PublishAsync(new CarrotMessage(header, string.Empty), CancellationToken.None);

        Assert.AreEqual(priority, _basicProperties.Priority);
    }

    [TestMethod]
    [DataRow("a13df78d-d615-46d7-9011-755ee983b061")]
    [DataRow(null)]
    public async Task BasicProperties_CorrelationId(string? id)
    {
        Guid? correlationId = null;
        if (Guid.TryParse(id, out var parsedId)) correlationId = parsedId;
        var header = new CarrotHeader { CorrelationId = correlationId };
        await _channel.PublishAsync(new CarrotMessage(header, string.Empty), CancellationToken.None);

        Assert.AreEqual(id, _basicProperties.CorrelationId);
    }

    [TestMethod]
    [DataRow(2000, "2000")]
    [DataRow(0, "0")]
    [DataRow(-1, null)]
    [DataRow(null, null)]
    public async Task BasicProperties_Expiration(int? ttl, string? result)
    {
        var header = new CarrotHeader { MessageProperties = new MessageProperties { Ttl = ttl } };
        await _channel.PublishAsync(new CarrotMessage(header, string.Empty), CancellationToken.None);

        Assert.AreEqual(result, _basicProperties.Expiration);
    }

    [TestMethod]
    public async Task BasicProperties_MessageId()
    {
        var messageId = Guid.Empty;
        var header = new CarrotHeader();
        await _channel.PublishAsync(new CarrotMessage(header, string.Empty), CancellationToken.None);

        Assert.AreEqual(messageId.ToString(), _basicProperties.MessageId);
    }

    [TestMethod]
    public async Task BasicProperties_AppId()
    {
        var appId = Guid.Empty;
        var header = new CarrotHeader { ServiceInstanceId = appId };
        await _channel.PublishAsync(new CarrotMessage(header, string.Empty), CancellationToken.None);

        Assert.AreEqual(appId.ToString(), _basicProperties.AppId);
    }
}