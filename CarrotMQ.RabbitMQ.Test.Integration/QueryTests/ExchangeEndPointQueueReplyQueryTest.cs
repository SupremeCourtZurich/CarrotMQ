using CarrotMQ.Core.EndPoints;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.QueryTests;

/// <summary>
/// Send query to ExchangeEndPoint with queue reply (to the same microservice)
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class ExchangeEndPointQueueReplyQueryTest : TestBaseAsyncReply
{
    [TestMethod]
    public async Task ExchangeEndPoint_QueueReply_OK()
    {
        const int id = 4101;

        await CarrotClient.SendAsync(
            new ExchangeEndPointQuery(id),
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyOk(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_QueueReply_Exception()
    {
        const int id = 4102;

        await CarrotClient.SendAsync(
            new ExchangeEndPointQuery(id) { ThrowException = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyException(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_QueueReply_Error()
    {
        const int id = 4103;

        await CarrotClient.SendAsync(
            new ExchangeEndPointQuery(id) { ReturnError = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyError(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_QueueReply_ErrorWithValidationErrors()
    {
        const int id = 4104;

        await CarrotClient.SendAsync(
            new ExchangeEndPointQuery(id) { ReturnErrorWithValidationErrors = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyErrorWithValidationErrors(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_QueueReply_CustomStatusCode()
    {
        const int id = 4105;

        await CarrotClient.SendAsync(
            new ExchangeEndPointQuery(id) { ReturnCustomStatusCode = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyCustomStatusCode(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_QueueReply_BadRequest()
    {
        const int id = 4106;

        await CarrotClient.SendAsync(
            new ExchangeEndPointQuery(id) { BadRequest = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyBadRequest(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_QueueReply_Reject()
    {
        const int id = 4107;

        await CarrotClient.SendAsync(
            new ExchangeEndPointQuery(id) { DoReject = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyDoReject(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_QueueReply_RetryDeadLetter()
    {
        const int id = 4108;

        await CarrotClient.SendAsync(
            new ExchangeEndPointQuery(id) { DoRetry = true },
            new QueueReplyEndPoint(TestQueue.Name));

        await VerifyDoRetry(id);
    }
}