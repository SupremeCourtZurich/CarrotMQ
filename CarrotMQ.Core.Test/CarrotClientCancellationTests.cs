using CarrotMQ.Core.Dto;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.MessageSending;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;
using CarrotMQ.Core.Test.Helper;
using NSubstitute;

namespace CarrotMQ.Core.Test;

/// <summary>
/// The transport fails with <see cref="PublishFailedException" /> when its token fires; the client tells the caller's
/// cancellation from the TTL.
/// </summary>
[TestClass]
public class CarrotClientCancellationTests
{
    private ICarrotClient _client = null!;
    private ITransport _transport = null!;

    [TestInitialize]
    public void Initialize()
    {
        _transport = Substitute.For<ITransport>();
        ICarrotSerializer serializer = new DefaultCarrotSerializer();
        _client = new CarrotClient(_transport, serializer, new CarrotMessageBuilder([], serializer, new DefaultRoutingKeyResolver()));
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task Publish_CallersTokenCancelled_ThrowsOperationCanceledExceptionWithTheReason()
    {
        // Arrange
        TransportFailsWhenCancelled(PublishFailureReason.Unreachable);
        using var cts = new CancellationTokenSource();
        var publish = _client.PublishAsync(new TestDto(1), messageProperties: new MessageProperties { Ttl = 60_000 }, cancellationToken: cts.Token);

        // Act
        cts.Cancel();

        // Assert
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => publish).ConfigureAwait(false);
        Assert.AreEqual(cts.Token, exception.CancellationToken);
        Assert.AreEqual(PublishFailureReason.Unreachable, ((PublishFailedException)exception.InnerException!).Reason);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task Publish_TtlExpires_ThrowsPublishFailedException()
    {
        // Arrange
        TransportFailsWhenCancelled(PublishFailureReason.Unreachable);

        // Act
        var publish = _client.PublishAsync(new TestDto(1), messageProperties: new MessageProperties { Ttl = 1 });

        // Assert
        var exception = await Assert.ThrowsExactlyAsync<PublishFailedException>(() => publish).ConfigureAwait(false);
        Assert.AreEqual(PublishFailureReason.Unreachable, exception.Reason);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task Publish_RefusedWhileTheCallersTokenIsCancelled_KeepsTheRefusal()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _transport.SendAsync(Arg.Any<CarrotMessage>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new RetryLimitExceededException("refused", PublishFailureReason.DestinationNotFound));

        // Act
        var publish = _client.PublishAsync(new TestDto(1), cancellationToken: cts.Token);

        // Assert
        var exception = await Assert.ThrowsExactlyAsync<RetryLimitExceededException>(() => publish).ConfigureAwait(false);
        Assert.AreEqual(PublishFailureReason.DestinationNotFound, exception.Reason);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task SendReceive_CallersTokenCancelled_ThrowsOperationCanceledExceptionWithTheReason()
    {
        // Arrange
        TransportFailsWhenCancelled(PublishFailureReason.Unreachable);
        using var cts = new CancellationTokenSource();
        ICommand<TestDto, TestResponse, TestQueueEndPoint> command = new TestDto(1);
        var send = _client.SendReceiveAsync(command, messageProperties: new MessageProperties { Ttl = 60_000 }, cancellationToken: cts.Token);

        // Act
        cts.Cancel();

        // Assert
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => send).ConfigureAwait(false);
        Assert.AreEqual(cts.Token, exception.CancellationToken);
        Assert.AreEqual(PublishFailureReason.Unreachable, ((PublishFailedException)exception.InnerException!).Reason);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task SendReceive_TtlExpires_ThrowsPublishFailedException()
    {
        // Arrange
        TransportFailsWhenCancelled(PublishFailureReason.Unreachable);
        ICommand<TestDto, TestResponse, TestQueueEndPoint> command = new TestDto(1);

        // Act
        var send = _client.SendReceiveAsync(command, messageProperties: new MessageProperties { Ttl = 1 });

        // Assert
        var exception = await Assert.ThrowsExactlyAsync<PublishFailedException>(() => send).ConfigureAwait(false);
        Assert.AreEqual(PublishFailureReason.Unreachable, exception.Reason);
    }

    // Like a transport waiting for an unreachable broker: it gives up only when its token fires.
    private void TransportFailsWhenCancelled(PublishFailureReason reason)
    {
        _transport.SendAsync(Arg.Any<CarrotMessage>(), Arg.Any<CancellationToken>())
            .Returns(ci => WaitForCancellationAsync<bool>(ci.Arg<CancellationToken>(), reason));
        _transport.SendReceiveAsync(Arg.Any<CarrotMessage>(), Arg.Any<CancellationToken>())
            .Returns(ci => WaitForCancellationAsync<CarrotMessage>(ci.Arg<CancellationToken>(), reason));
    }

    private static async Task<T> WaitForCancellationAsync<T>(CancellationToken token, PublishFailureReason reason)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            throw new PublishFailedException("cancelled", reason, ex);
        }

        throw new InvalidOperationException("Unreachable");
    }
}
