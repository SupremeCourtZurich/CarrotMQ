using CarrotMQ.Core.EndPoints;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.QueryTests;

/// <summary>
/// Send query to QueueEndPoint with exchange reply (to the same microservice)
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class QueueEndPointExchangeReplyQueryTest : TestBaseAsyncReply
{
    [TestMethod]
    public async Task QueueEndPoint_ExchangeReply_OK()
    {
        const int id = 4201;

        await CarrotClient.SendAsync(
            new QueueEndPointQuery(id),
            new ExchangeReplyEndPoint(TestExchange.Name, QueueEndPointQuery.Response.GetRoutingKey()));

        await VerifyOk(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_ExchangeReply_Exception()
    {
        const int id = 4202;

        await CarrotClient.SendAsync(
            new QueueEndPointQuery(id) { ThrowException = true },
            new ExchangeReplyEndPoint(TestExchange.Name, QueueEndPointQuery.Response.GetRoutingKey()));

        await VerifyException(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_ExchangeReply_Error()
    {
        const int id = 4203;

        await CarrotClient.SendAsync(
            new QueueEndPointQuery(id) { ReturnError = true },
            new ExchangeReplyEndPoint(TestExchange.Name, QueueEndPointQuery.Response.GetRoutingKey()));

        await VerifyError(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_ExchangeReply_ErrorWithValidationErrors()
    {
        const int id = 4204;

        await CarrotClient.SendAsync(
            new QueueEndPointQuery(id) { ReturnErrorWithValidationErrors = true },
            new ExchangeReplyEndPoint(TestExchange.Name, QueueEndPointQuery.Response.GetRoutingKey()));

        await VerifyErrorWithValidationErrors(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_ExchangeReply_CustomStatusCode()
    {
        const int id = 4205;

        await CarrotClient.SendAsync(
            new QueueEndPointQuery(id) { ReturnCustomStatusCode = true },
            new ExchangeReplyEndPoint(TestExchange.Name, QueueEndPointQuery.Response.GetRoutingKey()));

        await VerifyCustomStatusCode(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_ExchangeReply_BadRequest()
    {
        const int id = 4206;

        await CarrotClient.SendAsync(
            new QueueEndPointQuery(id) { BadRequest = true },
            new ExchangeReplyEndPoint(TestExchange.Name, QueueEndPointQuery.Response.GetRoutingKey()));

        await VerifyBadRequest(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_ExchangeReply_Reject()
    {
        const int id = 4207;

        await CarrotClient.SendAsync(
            new QueueEndPointQuery(id) { DoReject = true },
            new ExchangeReplyEndPoint(TestExchange.Name, QueueEndPointQuery.Response.GetRoutingKey()));

        await VerifyDoReject(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_ExchangeReply_RetryDeadLetter()
    {
        const int id = 4208;

        await CarrotClient.SendAsync(
            new QueueEndPointQuery(id) { DoRetry = true },
            new ExchangeReplyEndPoint(TestExchange.Name, QueueEndPointQuery.Response.GetRoutingKey()));

        await VerifyDoRetry(id);
    }
}