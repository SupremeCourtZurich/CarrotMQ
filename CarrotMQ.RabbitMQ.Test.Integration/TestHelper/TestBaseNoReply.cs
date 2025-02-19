namespace CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

public class TestBaseNoReply : TestBase
{
    /// <summary>
    /// - request is received and handled
    /// </summary>
    protected async Task VerifyOk(int id)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);
    }

    /// <summary>
    /// - request are all received and handled
    /// </summary>
    protected async Task VerifyOk(int startId, int count)
    {
        var receivedId = await ReceivedMessages.ReadAllAsync(count, Cts.Token).ConfigureAwait(false);

        for (int i = startId; i < startId + count; i++)
        {
            Assert.IsTrue(receivedId.Contains(i), $"{i} was not found in [{string.Join(",", receivedId)}]");
        }
    }

    /// <summary>
    /// - request is received and throws an exception
    /// - message is placed in dead letter queue
    /// </summary>
    protected async Task VerifyException(int id)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        receivedId = await DeadLetterConsumer.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId, nameof(DeadLetterConsumer));
    }

    /// <summary>
    /// - request is received and rejects it
    /// - message is placed in dead letter queue
    /// </summary>
    protected async Task VerifyDoReject(int id)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        var deadLetterId = await DeadLetterConsumer.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, deadLetterId, nameof(DeadLetterConsumer));
    }

    /// <summary>
    /// - request is received and rejects it with a retry
    /// - after four tries (1 + 3 retries) the message is placed in dead letter queue
    /// </summary>
    protected async Task VerifyDoRetry(int id)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);
        receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);
        receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);
        receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        var deadLetterId = await DeadLetterConsumer.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, deadLetterId, nameof(DeadLetterConsumer));
    }
}