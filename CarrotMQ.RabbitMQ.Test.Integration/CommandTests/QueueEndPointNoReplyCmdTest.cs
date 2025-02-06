using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.CommandTests;

/// <summary>
/// Send command to QueueEndPoint with NO reply
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class QueueEndPointNoReplyCmdTest : TestBaseNoReply
{
    [TestMethod]
    public async Task Queue_NoReply_OK()
    {
        const int id = 2301;

        await CarrotClient.SendAsync(new QueueEndPointCmd(id));

        await VerifyOk(id);
    }

    [TestMethod]
    public async Task Queue_NoReply_Exception()
    {
        const int id = 2302;

        await CarrotClient.SendAsync(new QueueEndPointCmd(id) { ThrowException = true });

        await VerifyException(id);
    }

    [TestMethod]
    public async Task Queue_NoReply_Reject()
    {
        const int id = 2307;

        await CarrotClient.SendAsync(new QueueEndPointCmd(id) { DoReject = true });

        await VerifyDoReject(id);
    }

    [TestMethod]
    public async Task Queue_NoReply_RetryDeadLetter()
    {
        const int id = 2308;

        await CarrotClient.SendAsync(new QueueEndPointCmd(id) { DoRetry = true });

        await VerifyDoRetry(id);
    }
}