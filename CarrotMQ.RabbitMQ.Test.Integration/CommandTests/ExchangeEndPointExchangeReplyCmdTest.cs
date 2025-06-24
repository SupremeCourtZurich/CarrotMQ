using CarrotMQ.Core;
using CarrotMQ.Core.EndPoints;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.CommandTests;

/// <summary>
/// Send command to ExchangeEndPoint with exchange reply (to the same microservice)
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class ExchangeEndPointExchangeReplyCmdTest : TestBaseAsyncReply
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ExchangeEndPoint_ExchangeReply_OK(bool publisherConfirm)
    {
        const int id = 3001;

        await CarrotClient.SendAsync(
            new ExchangeEndPointCmd(id),
            new ExchangeReplyEndPoint(TestExchange.Name, ExchangeEndPointCmd.Response.GetRoutingKey()),
            messageProperties: new MessageProperties { PublisherConfirm = publisherConfirm });

        await VerifyOk(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_ExchangeReply_Exception()
    {
        const int id = 3002;

        await CarrotClient.SendAsync(
            new ExchangeEndPointCmd(id) { ThrowException = true },
            new ExchangeReplyEndPoint(TestExchange.Name, ExchangeEndPointCmd.Response.GetRoutingKey()));

        await VerifyException(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_ExchangeReply_Error()
    {
        const int id = 3003;

        await CarrotClient.SendAsync(
            new ExchangeEndPointCmd(id) { ReturnError = true },
            new ExchangeReplyEndPoint(TestExchange.Name, ExchangeEndPointCmd.Response.GetRoutingKey()));

        await VerifyError(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_ExchangeReply_ErrorWithValidationErrors()
    {
        const int id = 3004;

        await CarrotClient.SendAsync(
            new ExchangeEndPointCmd(id) { ReturnErrorWithValidationErrors = true },
            new ExchangeReplyEndPoint(TestExchange.Name, ExchangeEndPointCmd.Response.GetRoutingKey()));

        await VerifyErrorWithValidationErrors(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_ExchangeReply_CustomStatusCode()
    {
        const int id = 3005;

        await CarrotClient.SendAsync(
            new ExchangeEndPointCmd(id) { ReturnCustomStatusCode = true },
            new ExchangeReplyEndPoint(TestExchange.Name, ExchangeEndPointCmd.Response.GetRoutingKey()));

        await VerifyCustomStatusCode(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_ExchangeReply_BadRequest()
    {
        const int id = 3006;

        await CarrotClient.SendAsync(
            new ExchangeEndPointCmd(id) { BadRequest = true },
            new ExchangeReplyEndPoint(TestExchange.Name, ExchangeEndPointCmd.Response.GetRoutingKey()));

        await VerifyBadRequest(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_ExchangeReply_Reject()
    {
        const int id = 3007;

        await CarrotClient.SendAsync(
            new ExchangeEndPointCmd(id) { DoReject = true },
            new ExchangeReplyEndPoint(TestExchange.Name, ExchangeEndPointCmd.Response.GetRoutingKey()));

        await VerifyDoReject(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_ExchangeReply_RetryDeadLetter()
    {
        const int id = 3008;

        await CarrotClient.SendAsync(
            new ExchangeEndPointCmd(id) { DoRetry = true },
            new ExchangeReplyEndPoint(TestExchange.Name, ExchangeEndPointCmd.Response.GetRoutingKey()));

        await VerifyDoRetry(id);
    }
}