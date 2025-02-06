using CarrotMQ.Core.EndPoints;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.CommandTests;

/// <summary>
/// Send command to QueueEndPoint with exchange reply (to the same microservice)
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class QueueEndPointExchangeReplyCmdTest : TestBaseAsyncReply
{
    [TestMethod]
    public async Task QueueEndPoint_ExchangeReply_OK()
    {
        const int id = 3201;

        await CarrotClient.SendAsync(
            new QueueEndPointCmd(id),
            new ExchangeReplyEndPoint(TestExchange.Name, QueueEndPointCmd.Response.GetRoutingKey()));

        await VerifyOk(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_ExchangeReply_Exception()
    {
        const int id = 3202;

        await CarrotClient.SendAsync(
            new QueueEndPointCmd(id) { ThrowException = true },
            new ExchangeReplyEndPoint(TestExchange.Name, QueueEndPointCmd.Response.GetRoutingKey()));

        await VerifyException(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_ExchangeReply_Error()
    {
        const int id = 3203;

        await CarrotClient.SendAsync(
            new QueueEndPointCmd(id) { ReturnError = true },
            new ExchangeReplyEndPoint(TestExchange.Name, QueueEndPointCmd.Response.GetRoutingKey()));

        await VerifyError(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_ExchangeReply_ErrorWithValidationErrors()
    {
        const int id = 3204;

        await CarrotClient.SendAsync(
            new QueueEndPointCmd(id) { ReturnErrorWithValidationErrors = true },
            new ExchangeReplyEndPoint(TestExchange.Name, QueueEndPointCmd.Response.GetRoutingKey()));

        await VerifyErrorWithValidationErrors(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_ExchangeReply_CustomStatusCode()
    {
        const int id = 3205;

        await CarrotClient.SendAsync(
            new QueueEndPointCmd(id) { ReturnCustomStatusCode = true },
            new ExchangeReplyEndPoint(TestExchange.Name, QueueEndPointCmd.Response.GetRoutingKey()));

        await VerifyCustomStatusCode(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_ExchangeReply_BadRequest()
    {
        const int id = 3206;

        await CarrotClient.SendAsync(
            new QueueEndPointCmd(id) { BadRequest = true },
            new ExchangeReplyEndPoint(TestExchange.Name, QueueEndPointCmd.Response.GetRoutingKey()));

        await VerifyBadRequest(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_ExchangeReply_Reject()
    {
        const int id = 3207;

        await CarrotClient.SendAsync(
            new QueueEndPointCmd(id) { DoReject = true },
            new ExchangeReplyEndPoint(TestExchange.Name, QueueEndPointCmd.Response.GetRoutingKey()));

        await VerifyDoReject(id);
    }

    [TestMethod]
    public async Task QueueEndPoint_ExchangeReply_RetryDeadLetter()
    {
        const int id = 3208;

        await CarrotClient.SendAsync(
            new QueueEndPointCmd(id) { DoRetry = true },
            new ExchangeReplyEndPoint(TestExchange.Name, QueueEndPointCmd.Response.GetRoutingKey()));

        await VerifyDoRetry(id);
    }
}