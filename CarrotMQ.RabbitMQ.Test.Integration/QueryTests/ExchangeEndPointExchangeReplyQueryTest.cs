using CarrotMQ.Core.EndPoints;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.QueryTests;

/// <summary>
/// Send query to ExchangeEndPoint with exchange reply (to the same microservice)
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class ExchangeEndPointExchangeReplyQueryTest : TestBaseAsyncReply
{
    [TestMethod]
    public async Task ExchangeEndPoint_ExchangeReply_OK()
    {
        const int id = 4001;

        await CarrotClient.SendAsync(
            new ExchangeEndPointQuery(id),
            new ExchangeReplyEndPoint(TestExchange.Name, ExchangeEndPointQuery.Response.GetRoutingKey()));

        await VerifyOk(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_ExchangeReply_Exception()
    {
        const int id = 4002;

        await CarrotClient.SendAsync(
            new ExchangeEndPointQuery(id) { ThrowException = true },
            new ExchangeReplyEndPoint(TestExchange.Name, ExchangeEndPointQuery.Response.GetRoutingKey()));

        await VerifyException(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_ExchangeReply_Error()
    {
        const int id = 4003;

        await CarrotClient.SendAsync(
            new ExchangeEndPointQuery(id) { ReturnError = true },
            new ExchangeReplyEndPoint(TestExchange.Name, ExchangeEndPointQuery.Response.GetRoutingKey()));

        await VerifyError(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_ExchangeReply_ErrorWithValidationErrors()
    {
        const int id = 4004;

        await CarrotClient.SendAsync(
            new ExchangeEndPointQuery(id) { ReturnErrorWithValidationErrors = true },
            new ExchangeReplyEndPoint(TestExchange.Name, ExchangeEndPointQuery.Response.GetRoutingKey()));

        await VerifyErrorWithValidationErrors(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_ExchangeReply_CustomStatusCode()
    {
        const int id = 4005;

        await CarrotClient.SendAsync(
            new ExchangeEndPointQuery(id) { ReturnCustomStatusCode = true },
            new ExchangeReplyEndPoint(TestExchange.Name, ExchangeEndPointQuery.Response.GetRoutingKey()));

        await VerifyCustomStatusCode(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_ExchangeReply_BadRequest()
    {
        const int id = 4006;

        await CarrotClient.SendAsync(
            new ExchangeEndPointQuery(id) { BadRequest = true },
            new ExchangeReplyEndPoint(TestExchange.Name, ExchangeEndPointQuery.Response.GetRoutingKey()));

        await VerifyBadRequest(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_ExchangeReply_Reject()
    {
        const int id = 4007;

        await CarrotClient.SendAsync(
            new ExchangeEndPointQuery(id) { DoReject = true },
            new ExchangeReplyEndPoint(TestExchange.Name, ExchangeEndPointQuery.Response.GetRoutingKey()));

        await VerifyDoReject(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_ExchangeReply_RetryDeadLetter()
    {
        const int id = 4008;

        await CarrotClient.SendAsync(
            new ExchangeEndPointQuery(id) { DoRetry = true },
            new ExchangeReplyEndPoint(TestExchange.Name, ExchangeEndPointQuery.Response.GetRoutingKey()));

        await VerifyDoRetry(id);
    }
}