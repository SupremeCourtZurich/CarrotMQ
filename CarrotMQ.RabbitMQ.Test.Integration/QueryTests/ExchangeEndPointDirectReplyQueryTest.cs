using CarrotMQ.Core;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.QueryTests;

/// <summary>
/// Send query to ExchangeEndPoint with direct reply (over channel)
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class ExchangeEndPointDirectReplyQueryTest : TestBaseDirectReply
{
    [TestMethod]
    public async Task ExchangeEndPoint_DirectReply_Ok()
    {
        const int id = 1101;

        var response = await CarrotClient.SendReceiveAsync(new ExchangeEndPointQuery(id));

        await VerifyOk(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_DirectReply_ThrowException()
    {
        const int id = 1102;

        var response = await CarrotClient.SendReceiveAsync(new ExchangeEndPointQuery(id) { ThrowException = true });

        await VerifyException(id, response);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_DirectReply_Error()
    {
        const int id = 1103;

        var response = await CarrotClient.SendReceiveAsync(new ExchangeEndPointQuery(id) { ReturnError = true });

        await VerifyError(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_DirectReply_ErrorWithValidationErrors()
    {
        const int id = 1104;

        var response = await CarrotClient.SendReceiveAsync(new ExchangeEndPointQuery(id) { ReturnErrorWithValidationErrors = true });

        await VerifyErrorWithValidationErrors(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_DirectReply_CustomStatusCode()
    {
        const int id = 1105;

        var response = await CarrotClient.SendReceiveAsync(new ExchangeEndPointQuery(id) { ReturnCustomStatusCode = true });

        await VerifyCustomStatusCode(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_DirectReply_BadRequest()
    {
        const int id = 1106;

        var response = await CarrotClient.SendReceiveAsync(new ExchangeEndPointQuery(id) { BadRequest = true });

        await VerifyBadRequest(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_DirectReply_Reject()
    {
        const int id = 1107;

        var response = await CarrotClient.SendReceiveAsync(new ExchangeEndPointQuery(id) { DoReject = true });

        await VerifyDoReject(response, id);
    }

    [TestMethod]
    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = true)]
    public async Task ExchangeEndPoint_DirectReply_Retry()
    {
        const int id = 1108;
        var sendTask = CarrotClient.SendReceiveAsync(new ExchangeEndPointQuery(id) { DoRetry = true });

        await VerifyDoRetry(id, sendTask);
    }

    [TestMethod]
    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = true)]
    public async Task ExchangeEndPoint_DirectReply_TimeOut()
    {
        const int id = 1109;
        const int timeoutMs = 550;

        var sendTask = CarrotClient.SendReceiveAsync(
            new ExchangeEndPointQuery(id)
            {
                TaskWaitDuration = TimeSpan.FromMilliseconds(timeoutMs),
                WaitDurationCount = 2
            },
            new Context(600));

        await VerifyOperationCanceled(id, sendTask);
    }
}