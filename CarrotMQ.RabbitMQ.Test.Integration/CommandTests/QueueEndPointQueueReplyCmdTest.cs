using CarrotMQ.Core.EndPoints;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.CommandTests;

/// <summary>
/// Send command to QueueEndPoint with queue reply (to the same microservice)
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class QueueEndPointQueueReplyCmdTest : TestBaseAsyncReply
{
    [TestMethod]
    public async Task QueueEndPoint_QueueReply_OK()
    {
        const int id = 3301;

        await CarrotClient.SendAsync(
            new QueueEndPointCmd(id),
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyOk(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_QueueReply_Exception()
    {
        const int id = 3302;

        await CarrotClient.SendAsync(
            new QueueEndPointCmd(id) { ThrowException = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyException(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_QueueReply_Error()
    {
        const int id = 3303;

        await CarrotClient.SendAsync(
            new QueueEndPointCmd(id) { ReturnError = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyError(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_QueueReply_ErrorWithValidationErrors()
    {
        const int id = 3304;

        await CarrotClient.SendAsync(
            new QueueEndPointCmd(id) { ReturnErrorWithValidationErrors = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyErrorWithValidationErrors(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_QueueReply_CustomStatusCode()
    {
        const int id = 3305;

        await CarrotClient.SendAsync(
            new QueueEndPointCmd(id) { ReturnCustomStatusCode = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyCustomStatusCode(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_QueueReply_BadRequest()
    {
        const int id = 3306;

        await CarrotClient.SendAsync(
            new QueueEndPointCmd(id) { BadRequest = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyBadRequest(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_QueueReply_Reject()
    {
        const int id = 3307;

        await CarrotClient.SendAsync(
            new QueueEndPointCmd(id) { DoReject = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyDoReject(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_QueueReply_RetryDeadLetter()
    {
        const int id = 3308;

        await CarrotClient.SendAsync(
            new QueueEndPointCmd(id) { DoRetry = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyDoRetry(id);
    }
}