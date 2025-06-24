using CarrotMQ.Core;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.CommandTests;

/// <summary>
/// Send command to ExchangeEndPoint with direct reply (over channel)
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class ExchangeEndPointDirectReplyCmdTest : TestBaseDirectReply
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ExchangeEndPoint_DirectReply_Ok(bool publisherConfirm)
    {
        const int id = 1001;

        var response = await CarrotClient.SendReceiveAsync(
            new ExchangeEndPointCmd(id),
            messageProperties: new MessageProperties { PublisherConfirm = publisherConfirm });

        await VerifyOk(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_DirectReply_ThrowException()
    {
        const int id = 1002;

        var response = await CarrotClient.SendReceiveAsync(new ExchangeEndPointCmd(id) { ThrowException = true });

        await VerifyException(id, response);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_DirectReply_Error()
    {
        const int id = 1003;

        var response = await CarrotClient.SendReceiveAsync(new ExchangeEndPointCmd(id) { ReturnError = true });

        await VerifyError(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_DirectReply_ErrorWithValidationErrors()
    {
        const int id = 1004;

        var response = await CarrotClient.SendReceiveAsync(new ExchangeEndPointCmd(id) { ReturnErrorWithValidationErrors = true });

        await VerifyErrorWithValidationErrors(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_DirectReply_CustomStatusCode()
    {
        const int id = 1005;

        var response = await CarrotClient.SendReceiveAsync(new ExchangeEndPointCmd(id) { ReturnCustomStatusCode = true });

        await VerifyCustomStatusCode(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_DirectReply_BadRequest()
    {
        const int id = 1006;

        var response = await CarrotClient.SendReceiveAsync(new ExchangeEndPointCmd(id) { BadRequest = true });

        await VerifyBadRequest(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_DirectReply_Reject()
    {
        const int id = 1007;

        var response = await CarrotClient.SendReceiveAsync(new ExchangeEndPointCmd(id) { DoReject = true });

        await VerifyDoReject(response, id);
    }

    [TestMethod]
    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = true)]
    public async Task ExchangeEndPoint_DirectReply_Retry()
    {
        const int id = 1008;

        var sendTask = CarrotClient.SendReceiveAsync(new ExchangeEndPointCmd(id) { DoRetry = true });

        await VerifyDoRetry(id, sendTask);
    }

    [TestMethod]
    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = true)]
    public async Task ExchangeEndPoint_DirectReply_TimeOut()
    {
        const int id = 1009;
        const int timeoutMs = 550;

        var sendTask = CarrotClient.SendReceiveAsync(
            new ExchangeEndPointCmd(id)
            {
                TaskWaitDuration = TimeSpan.FromMilliseconds(timeoutMs),
                WaitDurationCount = 2
            },
            messageProperties: new MessageProperties { Ttl = 600 });

        await VerifyOperationCanceled(id, sendTask);
    }
}