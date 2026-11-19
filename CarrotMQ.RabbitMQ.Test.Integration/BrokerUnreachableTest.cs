using CarrotMQ.Core;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace CarrotMQ.RabbitMQ.Test.Integration;

/// <summary>
/// A producer that never reached the broker fails like one whose connection was lost.
/// </summary>
/// <remarks>The producer points at a closed port, so every connection attempt is refused.</remarks>
[TestClass]
[TestCategory("Integration")]
public class BrokerUnreachableTest
{
    private static readonly Uri ClosedPort = new("amqp://127.0.0.1:1");

    // Longer than Wait.UntilAsync waits, so only the TTL or the caller's token can end the publish in time.
    private static readonly TimeSpan OutlastsTheWait = TimeSpan.FromSeconds(25);
    private CarrotHelper? _producer;

    [TestCleanup]
    public void Cleanup()
    {
        _producer?.Dispose();
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task NoConnection_EventTtlExpires_IsCancelledAsUnreachable()
    {
        // Arrange
        var client = StartProducer(OutlastsTheWait);

        // Act
        var publish = client.PublishAsync(new ExchangeEndPointEvent(1), messageProperties: WithTtl(500));

        // Assert
        await AssertCancelledAsUnreachableAsync(publish).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task NoConnection_QueryTtlExpires_IsCancelledAsUnreachable()
    {
        // Arrange
        var client = StartProducer(OutlastsTheWait);

        // Act
        var query = client.SendReceiveAsync(new ExchangeEndPointQuery(1), messageProperties: WithTtl(500));

        // Assert
        await AssertCancelledAsUnreachableAsync(query).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task NoConnection_CallerCancels_IsCancelledAsUnreachable()
    {
        // Arrange
        var client = StartProducer(OutlastsTheWait);
        using var cts = new CancellationTokenSource();
        var publish = client.PublishAsync(new ExchangeEndPointEvent(1), cancellationToken: cts.Token);

        // Act
        cts.Cancel();

        // Assert
        await AssertCancelledAsUnreachableAsync(publish).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task NoConnectionWithinInitialConnectionTimeout_FailsAsUnreachable()
    {
        // Arrange
        var client = StartProducer(TimeSpan.FromSeconds(1));

        // Act
        var publish = client.PublishAsync(new ExchangeEndPointEvent(1));

        // Assert
        await AssertUnreachableAsync(publish).ConfigureAwait(false);
    }

    private static MessageProperties WithTtl(int ttl)
    {
        return MessageProperties.Default with { Ttl = ttl };
    }

    private static async Task AssertUnreachableAsync(Task publish)
    {
        await Wait.UntilAsync(() => publish.IsCompleted, "the publish fails without a connection").ConfigureAwait(false);
        var exception = await Assert.ThrowsExactlyAsync<PublishFailedException>(() => publish).ConfigureAwait(false);
        Assert.AreEqual(PublishFailureReason.Unreachable, exception.Reason, exception.Message);
    }

    private static async Task AssertCancelledAsUnreachableAsync(Task publish)
    {
        await Wait.UntilAsync(() => publish.IsCompleted, "the publish is cancelled without a connection").ConfigureAwait(false);
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() => publish).ConfigureAwait(false);
        var failure = exception.InnerException as PublishFailedException;
        Assert.IsNotNull(failure, exception.ToString());
        Assert.AreEqual(PublishFailureReason.Unreachable, failure.Reason, failure.Message);
    }

    // The connect loop gives up after initialConnectionTimeout, so no attempt outlives the test.
    private ICarrotClient StartProducer(TimeSpan initialConnectionTimeout)
    {
        _producer = new CarrotHelper(
            "BrokerUnreachableTest.Producer",
            builder => builder.ConfigureBrokerConnection(
                configureOptions: options =>
                {
                    TestBase.ConfigureBroker(options);
                    options.BrokerEndPoints = [ClosedPort];
                    options.InitialConnectionTimeout = initialConnectionTimeout;
                }));

        return _producer.Host.Services.GetRequiredService<ICarrotClient>();
    }
}
