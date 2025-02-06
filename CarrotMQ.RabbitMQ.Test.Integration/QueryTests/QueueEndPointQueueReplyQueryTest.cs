using CarrotMQ.Core.EndPoints;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.QueryTests;

/// <summary>
/// Send query to QueueEndPoint with queue reply (to the same microservice)
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class QueueEndPointQueueReplyQueryTest : TestBaseAsyncReply
{
    [TestMethod]
    public async Task QueueEndPoint_QueueReply_OK()
    {
        const int id = 4301;

        await CarrotClient.SendAsync(
            new QueueEndPointQuery(id),
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyOk(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_QueueReply_Exception()
    {
        const int id = 4302;

        await CarrotClient.SendAsync(
            new QueueEndPointQuery(id) { ThrowException = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyException(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_QueueReply_Error()
    {
        const int id = 4303;

        await CarrotClient.SendAsync(
            new QueueEndPointQuery(id) { ReturnError = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyError(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_QueueReply_ErrorWithValidationErrors()
    {
        const int id = 4304;

        await CarrotClient.SendAsync(
            new QueueEndPointQuery(id) { ReturnErrorWithValidationErrors = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyErrorWithValidationErrors(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_QueueReply_CustomStatusCode()
    {
        const int id = 4305;

        await CarrotClient.SendAsync(
            new QueueEndPointQuery(id) { ReturnCustomStatusCode = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyCustomStatusCode(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_QueueReply_BadRequest()
    {
        const int id = 4306;

        await CarrotClient.SendAsync(
            new QueueEndPointQuery(id) { BadRequest = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyBadRequest(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_QueueReply_Reject()
    {
        const int id = 4307;

        await CarrotClient.SendAsync(
            new QueueEndPointQuery(id) { DoReject = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyDoReject(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_QueueReply_RetryDeadLetter()
    {
        const int id = 4308;

        await CarrotClient.SendAsync(
            new QueueEndPointQuery(id) { DoRetry = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyDoRetry(id);
    }
}