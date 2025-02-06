using CarrotMQ.Core.EndPoints;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.CommandTests;

/// <summary>
/// Send command to ExchangeEndPoint with queue reply (to the same microservice)
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class ExchangeEndPointQueueReplyCmdTest : TestBaseAsyncReply
{
    [TestMethod]
    public async Task ExchangeEndPoint_QueueReply_OK()
    {
        const int id = 3101;

        await CarrotClient.SendAsync(
            new ExchangeEndPointCmd(id),
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyOk(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_QueueReply_Exception()
    {
        const int id = 3102;

        await CarrotClient.SendAsync(
            new ExchangeEndPointCmd(id) { ThrowException = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyException(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_QueueReply_Error()
    {
        const int id = 3103;

        await CarrotClient.SendAsync(
            new ExchangeEndPointCmd(id) { ReturnError = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyError(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_QueueReply_ErrorWithValidationErrors()
    {
        const int id = 3104;

        await CarrotClient.SendAsync(
            new ExchangeEndPointCmd(id) { ReturnErrorWithValidationErrors = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyErrorWithValidationErrors(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_QueueReply_CustomStatusCode()
    {
        const int id = 3105;

        await CarrotClient.SendAsync(
            new ExchangeEndPointCmd(id) { ReturnCustomStatusCode = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyCustomStatusCode(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_QueueReply_BadRequest()
    {
        const int id = 3106;

        await CarrotClient.SendAsync(
            new ExchangeEndPointCmd(id) { BadRequest = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyBadRequest(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_QueueReply_Reject()
    {
        const int id = 3107;

        await CarrotClient.SendAsync(
            new ExchangeEndPointCmd(id) { DoReject = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyDoReject(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_QueueReply_RetryDeadLetter()
    {
        const int id = 3108;

        await CarrotClient.SendAsync(
            new ExchangeEndPointCmd(id) { DoRetry = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyDoRetry(id);
    }
}