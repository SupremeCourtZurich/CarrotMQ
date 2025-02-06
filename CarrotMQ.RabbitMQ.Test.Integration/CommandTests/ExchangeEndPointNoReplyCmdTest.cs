using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.CommandTests;

/// <summary>
/// Send command to ExchangeEndPoint with NO reply
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class ExchangeEndPointNoReplyCmdTest : TestBaseNoReply
{
    [TestMethod]
    public async Task ExchangeEndPoint_NoReply_OK()
    {
        const int id = 2201;

        await CarrotClient.SendAsync(new ExchangeEndPointCmd(id));

        await VerifyOk(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_NoReply_Exception()
    {
        const int id = 2202;

        await CarrotClient.SendAsync(new ExchangeEndPointCmd(id) { ThrowException = true });

        await VerifyException(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_NoReply_Reject()
    {
        const int id = 2207;

        await CarrotClient.SendAsync(new ExchangeEndPointCmd(id) { DoReject = true });

        await VerifyDoReject(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_NoReply_RetryDeadLetter()
    {
        const int id = 2208;

        await CarrotClient.SendAsync(new ExchangeEndPointCmd(id) { DoRetry = true });

        await VerifyDoRetry(id);
    }
}