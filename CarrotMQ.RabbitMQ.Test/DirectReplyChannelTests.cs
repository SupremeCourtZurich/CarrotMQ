using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Serialization;
using CarrotMQ.RabbitMQ.Test.Helper;
using NSubstitute;
using RabbitMQ.Client;

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class DirectReplyChannelTests
{
    private IChannel _channel = null!;
    private IDirectReplyChannel _sut = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _channel = Substitute.For<IChannel>();
        _channel.IsOpen.Returns(true);
        var connection = Substitute.For<IConnection>();
        connection.IsOpen.Returns(true);
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>(), Arg.Any<CancellationToken>()).Returns(_channel);

        _sut = await DirectReplyChannel.CreateAsync(connection, TimeSpan.FromMilliseconds(20), CreateSerializer(), TestLoggerFactory.Instance)
            .ConfigureAwait(false);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _sut.DisposeAsync().ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task RequestFailedToPublish_RetryWithTheSameCorrelationId_GetsItsReply()
    {
        // Arrange
        var correlationId = Guid.NewGuid();
        var failNextPublish = true;
        _channel.When(async c => await c.BasicPublishAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Any<BasicProperties>(),
                    Arg.Any<ReadOnlyMemory<byte>>(),
                    Arg.Any<CancellationToken>())
                .ConfigureAwait(false))
            .Do(_ =>
            {
                if (!failNextPublish) return;

                failNextPublish = false;
                throw new InvalidOperationException("publish failed");
            });
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => _sut.PublishWithReplyAsync(new CarrotMessage(new CarrotHeader { CorrelationId = correlationId }, "request"), CancellationToken.None))
            .ConfigureAwait(false);

        // Act
        var retry = _sut.PublishWithReplyAsync(new CarrotMessage(new CarrotHeader { CorrelationId = correlationId }, "request"), CancellationToken.None);
        await Wait.UntilAsync(() => CountPublishes() >= 2, "the retry is published").ConfigureAwait(false);
        await DeliverReplyAsync(correlationId).ConfigureAwait(false);

        // Assert
        await Wait.UntilAsync(() => retry.IsCompleted, "the retry gets its reply").ConfigureAwait(false);
        var reply = await retry.ConfigureAwait(false);
        Assert.AreEqual(correlationId, reply.Header.CorrelationId);
    }

    private int CountPublishes()
    {
        return _channel.ReceivedCalls().Count(c => string.Equals(c.GetMethodInfo().Name, nameof(IChannel.BasicPublishAsync), StringComparison.Ordinal));
    }

    private static IProtocolSerializer CreateSerializer()
    {
        var serializer = Substitute.For<IProtocolSerializer>();
        serializer.Serialize(Arg.Any<CarrotMessage>(), Arg.Any<BasicProperties>()).Returns("payload");
        serializer.Deserialize(Arg.Any<string>(), Arg.Any<IReadOnlyBasicProperties>())
            .Returns(ci => new CarrotMessage(new CarrotHeader { CorrelationId = Guid.Parse(ci.ArgAt<IReadOnlyBasicProperties>(1).CorrelationId!) }, "reply"));

        return serializer;
    }

    // Delivers through the consumer the channel registered on amq.rabbitmq.reply-to.
    private async Task DeliverReplyAsync(Guid correlationId)
    {
        var consumer = _channel.ReceivedCalls()
            .SelectMany(c => c.GetArguments())
            .OfType<IAsyncBasicConsumer>()
            .Single();

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
}
