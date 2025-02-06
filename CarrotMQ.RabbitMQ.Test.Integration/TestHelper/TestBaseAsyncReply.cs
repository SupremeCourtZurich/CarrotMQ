using CarrotMQ.Core.Protocol;

namespace CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

[TestClass]
public class TestBaseAsyncReply : TestBase
{
    /// <summary>
    /// - request is received and handled
    /// - response is received.
    /// </summary>
    protected async Task VerifyOk(int id)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        var receivedResponse = await ReceivedResponses.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedResponse.Id);
        Assert.AreEqual(CarrotStatusCode.Ok, receivedResponse.StatusCode);
    }

    /// <summary>
    /// - request is received and throws an exception
    /// - response with 500 internal server error is received
    /// - message is placed in dead letter queue
    /// </summary>
    protected async Task VerifyException(int id)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        var receivedResponse = await ReceivedResponses.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(CarrotStatusCode.InternalServerError, receivedResponse.StatusCode);

        receivedId = await DeadLetterConsumer.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId, nameof(DeadLetterConsumer));
    }

    /// <summary>
    /// - request is received and returns an error
    /// - response with 500 internal server error is received and it contains an error message and a response value
    /// </summary>
    protected async Task VerifyError(int id)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        var receivedResponse = await ReceivedResponses.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedResponse.Id);
        Assert.AreEqual(CarrotStatusCode.InternalServerError, receivedResponse.StatusCode);
        Assert.AreEqual($"Error for {id}", receivedResponse.Error!.Message);
    }

    /// <summary>
    /// - request is received and returns an error
    /// - response with 500 internal server error is received and it contains a list of error messages
    /// </summary>
    protected async Task VerifyErrorWithValidationErrors(int id)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        var receivedResponse = await ReceivedResponses.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(CarrotStatusCode.InternalServerError, receivedResponse.StatusCode);
        Assert.AreEqual($"CustomError {id}", receivedResponse.Error!.Message);
        Assert.AreEqual("Error1.Message1", receivedResponse.Error!.Errors["Error1"][0]);
        Assert.AreEqual("Error1.Message2", receivedResponse.Error!.Errors["Error1"][1]);
    }

    /// <summary>
    /// - request is received and returns an error
    /// - response with custom status code is received and it contains a response value
    /// </summary>
    protected async Task VerifyCustomStatusCode(int id)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        var receivedResponse = await ReceivedResponses.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedResponse.Id);
        Assert.AreEqual(999, receivedResponse.StatusCode);
    }

    /// <summary>
    /// - request is received and returns an error
    /// - response with 400 bad request is received and it contains an error message and a response value
    /// </summary>
    protected async Task VerifyBadRequest(int id)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        var receivedResponse = await ReceivedResponses.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedResponse.Id);
        Assert.AreEqual(CarrotStatusCode.BadRequest, receivedResponse.StatusCode);
        Assert.AreEqual("Validation error", receivedResponse.Error!.Message);
    }

    /// <summary>
    /// - request is received and rejects it
    /// - response with 500 internal server error is received
    /// - message is placed in dead letter queue
    /// </summary>
    protected async Task VerifyDoReject(int id)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        var receivedResponse = await ReceivedResponses.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(CarrotStatusCode.InternalServerError, receivedResponse.StatusCode);

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