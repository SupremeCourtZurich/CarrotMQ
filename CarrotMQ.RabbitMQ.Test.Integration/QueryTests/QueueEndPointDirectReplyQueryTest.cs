using CarrotMQ.Core;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.QueryTests;

/// <summary>
/// Send query to QueueEndPoint with direct reply (over channel)
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class QueueEndPointDirectReplyQueryTest : TestBaseDirectReply
{
    [TestMethod]
    public async Task QueueEndPoint_DirectReply_Ok()
    {
        const int id = 1301;

        var response = await CarrotClient.SendReceiveAsync(new QueueEndPointQuery(id));

        await VerifyOk(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task QueueEndPoint_DirectReply_ThrowException()
    {
        const int id = 1302;

        var response = await CarrotClient.SendReceiveAsync(new QueueEndPointQuery(id) { ThrowException = true });

        await VerifyException(id, response);
    }

    [TestMethod]
    public async Task QueueEndPoint_DirectReply_Error()
    {
        const int id = 1303;

        var response = await CarrotClient.SendReceiveAsync(new QueueEndPointQuery(id) { ReturnError = true });

        await VerifyError(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task QueueEndPoint_DirectReply_ErrorWithValidationErrors()
    {
        const int id = 1304;

        var response = await CarrotClient.SendReceiveAsync(new QueueEndPointQuery(id) { ReturnErrorWithValidationErrors = true });

        await VerifyErrorWithValidationErrors(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task QueueEndPoint_DirectReply_CustomStatusCode()
    {
        const int id = 1305;

        var response = await CarrotClient.SendReceiveAsync(new QueueEndPointQuery(id) { ReturnCustomStatusCode = true });

        await VerifyCustomStatusCode(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task QueueEndPoint_DirectReply_BadRequest()
    {
        const int id = 1306;

        var response = await CarrotClient.SendReceiveAsync(new QueueEndPointQuery(id) { BadRequest = true });

        await VerifyBadRequest(id, response, response.Content?.Id);
    }

    [TestMethod]
    public async Task QueueEndPoint_DirectReply_Reject()
    {
        const int id = 1307;

        var response = await CarrotClient.SendReceiveAsync(new QueueEndPointQuery(id) { DoReject = true });

        await VerifyDoReject(response, id);
    }

    [TestMethod]
    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = true)]
    public async Task QueueEndPoint_DirectReply_RetryError()
    {
        const int id = 1308;

        var sendTask = CarrotClient.SendReceiveAsync(new QueueEndPointQuery(id) { DoRetry = true });

        await VerifyDoRetry(id, sendTask);
    }

    [TestMethod]
    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = true)]
    public async Task QueueEndPoint_DirectReply_TimeOut()
    {
        const int id = 1309;
        const int timeoutMs = 550;

        var sendTask = CarrotClient.SendReceiveAsync(
            new QueueEndPointQuery(id)
            {
                TaskWaitDuration = TimeSpan.FromMilliseconds(timeoutMs),
                WaitDurationCount = 2
            },
            messageProperties: new MessageProperties { Ttl = 600 });

        await VerifyOperationCanceled(id, sendTask);
    }
}