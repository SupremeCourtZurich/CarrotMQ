using CarrotMQ.Core.Common;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core.Test.Common;

[TestClass]
public class CarrotMessageHasReplyTest
{
    [TestMethod]
    [DataRow("ReplyExchange", "", true)]
    [DataRow("ReplyExchange", null, true)]
    [DataRow("ReplyExchange", "   ", true)]
    [DataRow("", "ReplyRoutingKey", true)]
    [DataRow(null, "ReplyRoutingKey", true)]
    [DataRow("   ", "ReplyRoutingKey", true)]
    [DataRow("", "", false)]
    [DataRow(null, null, false)]
    [DataRow("ReplyExchange", "ReplyRoutingKey", true)]
    public void CarrotMessage_ExchangeOnly_HasReply(string replyExchange, string replyRoutingKey, bool expected)
    {
        var carrotMessage = new CarrotMessage(
            new CarrotHeader
            {
                ReplyExchange = replyExchange,
                ReplyRoutingKey = replyRoutingKey
            },
            "");

        Assert.AreEqual(expected, carrotMessage.HasReply());
    }
}