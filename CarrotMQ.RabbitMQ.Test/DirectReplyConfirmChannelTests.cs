using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Serialization;
using CarrotMQ.RabbitMQ.Test.Helper;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using AckAsyncEventHandler = RabbitMQ.Client.Events.AsyncEventHandler<RabbitMQ.Client.Events.BasicAckEventArgs>;
using ShutdownAsyncEventHandler = RabbitMQ.Client.Events.AsyncEventHandler<RabbitMQ.Client.Events.ShutdownEventArgs>;

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class DirectReplyConfirmChannelTests
{
    private static readonly ShutdownEventArgs Refusal = new(ShutdownInitiator.Peer, 404, "NOT_FOUND", 60, 40);

    private readonly object _lock = new();
    private readonly List<string> _log = [];
    private IChannel _mainChannel = null!;
    private IChannel _recoveryChannel = null!;
    private IDirectReplyChannel _sut = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _mainChannel = CreateChannel("main", ackEveryPublish: false);
        _recoveryChannel = CreateChannel("recovery", ackEveryPublish: true);
        _sut = await DirectReplyConfirmChannel.CreateAsync(
                CreateConnection(),
                TimeSpan.FromMilliseconds(20),
                new PublisherConfirmOptions { RetryLimit = 5 },
                CreateSerializer(),
                TestLoggerFactory.Instance)
            .ConfigureAwait(false);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _sut.DisposeAsync().ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task RequestConfirmedOnTheRecoveryChannel_GetsItsReplyThereAndTheNextMessageWaitsForIt()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var request = _sut.PublishWithReplyAsync(new CarrotMessage(new CarrotHeader { CorrelationId = correlationId }, "request"), CancellationToken.None);
        var next = _sut.PublishAsync(new CarrotMessage(new CarrotHeader(), "next"), CancellationToken.None);
        await WaitForPublishesAsync(_mainChannel, 2).ConfigureAwait(false);

        // Act
        RefuseMainChannel();
        await WaitForPublishesAsync(_recoveryChannel, 1).ConfigureAwait(false);
        await DeliverReplyOnTheRecoveryChannelAsync(correlationId).ConfigureAwait(false);
        var reply = await request.ConfigureAwait(false);
        await next.ConfigureAwait(false);

        // Assert
        Assert.AreEqual(correlationId, reply.Header.CorrelationId);
        AssertLog("publish main", "publish main", "publish recovery", "ack recovery", "reply recovery", "publish recovery", "ack recovery");
    }

    // A channel that records its publishes and, if asked, acks each one before the publish returns.
    private IChannel CreateChannel(string name, bool ackEveryPublish)
    {
        var channel = Substitute.For<IChannel>();
        channel.IsOpen.Returns(true);
        long seqNo = 0;
        channel.GetNextPublishSequenceNumberAsync(Arg.Any<CancellationToken>()).Returns(_ => (ulong)Interlocked.Increment(ref seqNo));
        channel.When(async c => await c.BasicPublishAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Any<BasicProperties>(),
                    Arg.Any<ReadOnlyMemory<byte>>(),
                    Arg.Any<CancellationToken>())
                .ConfigureAwait(false))
            .Do(_ =>
            {
                Record($"publish {name}");
                if (!ackEveryPublish) return;

                Record($"ack {name}");
                channel.BasicAcksAsync += Raise.Event<AckAsyncEventHandler>(channel, new BasicAckEventArgs((ulong)Interlocked.Read(ref seqNo), false));
            });

        return channel;
    }

    // The recovery channel is opened with its cancellable dispose token, the main channel without a token.
    private IConnection CreateConnection()
    {
        var connection = Substitute.For<IConnection>();
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<CancellationToken>(1).CanBeCanceled ? _recoveryChannel : _mainChannel);

        return connection;
    }

    private static IProtocolSerializer CreateSerializer()
    {
        var serializer = Substitute.For<IProtocolSerializer>();
        serializer.Serialize(Arg.Any<CarrotMessage>(), Arg.Any<BasicProperties>()).Returns("payload");
        serializer.Deserialize(Arg.Any<string>(), Arg.Any<IReadOnlyBasicProperties>())
            .Returns(ci => new CarrotMessage(new CarrotHeader { CorrelationId = Guid.Parse(ci.ArgAt<IReadOnlyBasicProperties>(1).CorrelationId!) }, "reply"));

        return serializer;
    }

    private void RefuseMainChannel()
    {
        var closedChecks = 2;
        _mainChannel.IsOpen.Returns(_ => Interlocked.Decrement(ref closedChecks) < 0);
        _mainChannel.ChannelShutdownAsync += Raise.Event<ShutdownAsyncEventHandler>(null, Refusal);
    }

    // Delivers through the consumer the recovery channel registered on amq.rabbitmq.reply-to.
    private async Task DeliverReplyOnTheRecoveryChannelAsync(Guid correlationId)
    {
        var consumer = _recoveryChannel.ReceivedCalls()
            .SelectMany(c => c.GetArguments())
            .OfType<IAsyncBasicConsumer>()
            .Single();

        Record("reply recovery");
        await consumer.HandleBasicDeliverAsync(
                "consumer",
                1,
                false,
                string.Empty,
                "amq.rabbitmq.reply-to",
                new BasicProperties { CorrelationId = correlationId.ToString() },
                ReadOnlyMemory<byte>.Empty)
            .ConfigureAwait(false);
    }

    private void Record(string entry)
    {
        lock (_lock)
        {
            _log.Add(entry);
        }
    }

    private void AssertLog(params string[] expected)
    {
        List<string> log;
        lock (_lock)
        {
            log = [.. _log];
        }

        CollectionAssert.AreEqual(expected, log, string.Join(", ", log));
    }

    private static async Task WaitForPublishesAsync(IChannel channel, int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(4);
        while (channel.ReceivedCalls().Count(c => string.Equals(c.GetMethodInfo().Name, nameof(IChannel.BasicPublishAsync), StringComparison.Ordinal)) < count)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"{count} publishes not seen within 4 s");

            await Task.Delay(10).ConfigureAwait(false);
        }
    }
}