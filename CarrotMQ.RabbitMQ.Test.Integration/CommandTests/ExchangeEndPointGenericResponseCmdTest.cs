using CarrotMQ.Core;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.CommandTests;

/// <summary>
/// Send command to ExchangeEndPoint with direct reply (over channel)
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class ExchangeEndPointGenericResponseCmdTest : TestBaseDirectReply
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ExchangeEndPoint_DirectReply_Ok(bool publisherConfirm)
    {
        const int id = 1001;

        var response = await CarrotClient.SendReceiveAsync(
            new ExchangeEndPointGenericResponseCmd(id),
            messageProperties: new MessageProperties { PublisherConfirm = publisherConfirm });

        await VerifyOk(id, response, response.Content?.InnerResponse?.Id);
    }
}