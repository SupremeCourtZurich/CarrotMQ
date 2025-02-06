using CarrotMQ.Core;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

public class TestBaseDirectReply : TestBase
{
    /// <summary>
    /// - request is received and handled
    /// - response with 200 ok is received.
    /// </summary>
    protected async Task VerifyOk(int id, CarrotResponse response, int? responseId)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        Assert.AreEqual(CarrotStatusCode.Ok, response.StatusCode, nameof(response.StatusCode));
        Assert.AreEqual(id, responseId);
    }

    /// <summary>
    /// - request is received and throws an exception
    /// - response with 500 internal server error is received
    /// - message is placed in dead letter queue
    /// </summary>
    protected async Task VerifyException(int id, CarrotResponse response)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        Assert.AreEqual(CarrotStatusCode.InternalServerError, response.StatusCode, nameof(response.StatusCode));

        receivedId = await DeadLetterConsumer.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId, nameof(DeadLetterConsumer));
    }

    /// <summary>
    /// - request is received and returns an error
    /// - response with 500 internal server error is received and it contains an error message and a response value
    /// </summary>
    protected async Task VerifyError(int id, CarrotResponse response, int? responseId)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        Assert.AreEqual(CarrotStatusCode.InternalServerError, response.StatusCode, nameof(response.StatusCode));
        Assert.AreEqual($"Error for {id}", response.Error?.Message);
        Assert.AreEqual(id, responseId);
    }

    /// <summary>
    /// - request is received and returns an error
    /// - response with 500 internal server error is received and it contains a list of error messages
    /// </summary>
    protected async Task VerifyErrorWithValidationErrors(int id, CarrotResponse response, int? responseId)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        Assert.IsNotNull(response);
        Assert.AreEqual(CarrotStatusCode.InternalServerError, response.StatusCode, nameof(response.StatusCode));
        Assert.AreEqual($"CustomError {id}", response.Error!.Message);
        Assert.AreEqual("Error1.Message1", response.Error!.Errors["Error1"][0]);
        Assert.AreEqual("Error1.Message2", response.Error!.Errors["Error1"][1]);
        Assert.IsNull(responseId);
    }

    /// <summary>
    /// - request is received and returns an error
    /// - response with custom status code is received and it contains a response value
    /// </summary>
    protected async Task VerifyCustomStatusCode(int id, CarrotResponse response, int? responseId)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        Assert.IsNotNull(response);
        Assert.AreEqual(999, response.StatusCode, nameof(response.StatusCode));
        Assert.IsNull(response.Error?.Message);
        Assert.AreEqual(id, responseId);
    }

    /// <summary>
    /// - request is received and returns an error
    /// - response with 400 bad request is received and it contains an error message and a response value
    /// </summary>
    protected async Task VerifyBadRequest(int id, CarrotResponse response, int? responseId)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        Assert.IsNotNull(response);
        Assert.AreEqual(CarrotStatusCode.BadRequest, response.StatusCode, nameof(response.StatusCode));
        Assert.AreEqual("Validation error", response.Error?.Message);
        Assert.AreEqual(id, responseId);
    }

    /// <summary>
    /// - request is received and rejects it
    /// - response with 500 internal server error is received
    /// - message is placed in dead letter queue
    /// </summary>
    protected async Task VerifyDoReject(CarrotResponse response, int id)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        Assert.AreEqual(CarrotStatusCode.InternalServerError, response.StatusCode, nameof(response.StatusCode));

        receivedId = await DeadLetterConsumer.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId, nameof(DeadLetterConsumer));
    }

    /// <summary>
    /// - request is received and rejects it with a retry
    /// - after four tries the message is placed in dead letter queue
    /// - the send task throws a <see cref="OperationCanceledException" />
    /// </summary>
    protected async Task VerifyDoRetry(int id, Task sendTask)
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

        await sendTask.ConfigureAwait(false);
    }

    /// <summary>
    /// - request is received and takes a long time to process
    /// - it takes a long time to process
    /// - the
    /// <param name="sendTask">send task</param>
    /// throws a <see cref="OperationCanceledException" />
    /// </summary>
    protected async Task VerifyOperationCanceled(int id, Task sendTask)
    {
        var receivedId = await ReceivedMessages.ReadAsync(Cts.Token).ConfigureAwait(false);
        Assert.AreEqual(id, receivedId);

        await sendTask.ConfigureAwait(false);
    }
}