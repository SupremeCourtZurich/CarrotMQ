using CarrotMQ.Core;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.CommandTests;

/// <summary>
/// Send command to QueueEndPoint with direct reply (over channel)
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class QueueEndPointDirectReplyCmdTest : TestBaseDirectReply
{
    [TestMethod]
    public async Task QueueEndPoint_DirectReply_Ok()
    {
        const int id = 1201;

        var response = await CarrotClient.SendReceiveAsync(new QueueEndPointCmd(id));

        await VerifyOk(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task QueueEndPoint_DirectReply_ThrowException()
    {
        const int id = 1202;

        var response = await CarrotClient.SendReceiveAsync(new QueueEndPointCmd(id) { ThrowException = true });

        await VerifyException(id, response);
    }

    [TestMethod]
    public async Task QueueEndPoint_DirectReply_Error()
    {
        const int id = 1203;

        var response = await CarrotClient.SendReceiveAsync(new QueueEndPointCmd(id) { ReturnError = true });

        await VerifyError(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task QueueEndPoint_DirectReply_ErrorWithValidationErrors()
    {
        const int id = 1204;

        var response = await CarrotClient.SendReceiveAsync(new QueueEndPointCmd(id) { ReturnErrorWithValidationErrors = true });

        await VerifyErrorWithValidationErrors(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task QueueEndPoint_DirectReply_CustomStatusCode()
    {
        const int id = 1205;

        var response = await CarrotClient.SendReceiveAsync(new QueueEndPointCmd(id) { ReturnCustomStatusCode = true });

        await VerifyCustomStatusCode(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task QueueEndPoint_DirectReply_BadRequest()
    {
        const int id = 1206;

        var response = await CarrotClient.SendReceiveAsync(new QueueEndPointCmd(id) { BadRequest = true });

        await VerifyBadRequest(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task QueueEndPoint_DirectReply_Reject()
    {
        const int id = 1207;

        var response = await CarrotClient.SendReceiveAsync(new QueueEndPointCmd(id) { DoReject = true });

        await VerifyDoReject(response, id);
    }

    [TestMethod]
    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = true)]
    public async Task QueueEndPoint_DirectReply_RetryError()
    {
        const int id = 1208;

        var sendTask = CarrotClient.SendReceiveAsync(new QueueEndPointCmd(id) { DoRetry = true });

        await VerifyDoRetry(id, sendTask);
    }

    [TestMethod]
    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = true)]
    public async Task QueueEndPoint_DirectReply_TimeOut()
    {
        const int id = 1209;
        const int timeoutMs = 550;

        var sendTask = CarrotClient.SendReceiveAsync(
            new QueueEndPointCmd(id)
            {
                TaskWaitDuration = TimeSpan.FromMilliseconds(timeoutMs),
                WaitDurationCount = 2
            },
            messageProperties: new MessageProperties { Ttl = 600 });

        await VerifyOperationCanceled(id, sendTask);
    }
}