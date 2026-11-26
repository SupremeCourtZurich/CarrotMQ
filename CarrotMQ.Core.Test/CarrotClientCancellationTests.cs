using CarrotMQ.Core.Dto;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.MessageSending;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;
using CarrotMQ.Core.Test.Helper;
using NSubstitute;

namespace CarrotMQ.Core.Test;

/// <summary>
/// The transport fails with an <see cref="OperationCanceledException" /> carrying a <see cref="PublishFailedException" /> when
/// its token fires; the client passes it on with the caller's token when the caller cancelled.
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
        Assert.IsTrue(exception.CancellationToken.IsCancellationRequested, "The exception carries the cancelled token.");
        Assert.AreEqual(PublishFailureReason.Unreachable, ((PublishFailedException)exception.InnerException!).Reason);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task Publish_TtlExpires_ThrowsOperationCanceledExceptionWithTheReason()
    {
        // Arrange
        TransportFailsWhenCancelled(PublishFailureReason.Unreachable);

        // Act
        var publish = _client.PublishAsync(new TestDto(1), messageProperties: new MessageProperties { Ttl = 1 });

        // Assert
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => publish).ConfigureAwait(false);
        Assert.AreEqual(PublishFailureReason.Unreachable, ((PublishFailedException)exception.InnerException!).Reason);
        Assert.IsTrue(exception.CancellationToken.CanBeCanceled, "The exception carries the TTL token.");
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
        Assert.IsTrue(exception.CancellationToken.IsCancellationRequested, "The exception carries the cancelled token.");
        Assert.AreEqual(PublishFailureReason.Unreachable, ((PublishFailedException)exception.InnerException!).Reason);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task SendReceive_TtlExpires_ThrowsOperationCanceledExceptionWithTheReason()
    {
        // Arrange
        TransportFailsWhenCancelled(PublishFailureReason.Unreachable);
        ICommand<TestDto, TestResponse, TestQueueEndPoint> command = new TestDto(1);

        // Act
        var send = _client.SendReceiveAsync(command, messageProperties: new MessageProperties { Ttl = 1 });

        // Assert
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => send).ConfigureAwait(false);
        Assert.AreEqual(PublishFailureReason.Unreachable, ((PublishFailedException)exception.InnerException!).Reason);
        Assert.IsTrue(exception.CancellationToken.CanBeCanceled, "The exception carries the TTL token.");
    }

    [TestMethod]
    [Timeout(5_000)]
    [DataRow(null)]
    [DataRow(0)]
    [DataRow(-1)]
    public async Task SendReceive_NoPositiveTtl_WaitIsBoundedAndTheHeaderKeepsTheTtl(int? ttl)
    {
        // Arrange
        CancellationToken sentWith = default;
        CarrotMessage? sent = null;
        _transport.SendReceiveAsync(Arg.Do<CarrotMessage>(m => sent = m), Arg.Do<CancellationToken>(t => sentWith = t))
            .Returns(_ => Task.FromException<CarrotMessage>(new PublishFailedException("stop", PublishFailureReason.Unknown)));
        ICommand<TestDto, TestResponse, TestQueueEndPoint> command = new TestDto(1);

        // Act
        await Assert.ThrowsAsync<PublishFailedException>(() => _client.SendReceiveAsync(command, messageProperties: new MessageProperties { Ttl = ttl }))
            .ConfigureAwait(false);

        // Assert
        Assert.IsTrue(sentWith.CanBeCanceled, "The wait for the reply has no bound.");
        Assert.AreEqual(ttl ?? 5_000, sent!.Header.MessageProperties.Ttl);
    }

    [TestMethod]
    [Timeout(5_000)]
    [DataRow(0)]
    [DataRow(-1)]
    public async Task SendReceive_NoPositiveTtlWithTheCallersToken_WaitsForTheCallersToken(int ttl)
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        CancellationToken sentWith = default;
        _transport.SendReceiveAsync(Arg.Any<CarrotMessage>(), Arg.Do<CancellationToken>(t => sentWith = t))
            .Returns(_ => Task.FromException<CarrotMessage>(new PublishFailedException("stop", PublishFailureReason.Unknown)));
        ICommand<TestDto, TestResponse, TestQueueEndPoint> command = new TestDto(1);

        // Act
        await Assert.ThrowsAsync<PublishFailedException>(
                () => _client.SendReceiveAsync(command, messageProperties: new MessageProperties { Ttl = ttl }, cancellationToken: cts.Token))
            .ConfigureAwait(false);

        // Assert
        Assert.AreEqual(cts.Token, sentWith);
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
            throw new OperationCanceledException("cancelled", new PublishFailedException("cancelled", reason, ex), token);
        }

        throw new InvalidOperationException("Unreachable");
    }
}
