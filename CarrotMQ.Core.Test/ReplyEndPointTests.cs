using CarrotMQ.Core.EndPoints;

namespace CarrotMQ.Core.Test;

[TestClass]
public sealed class ReplyEndPointTests
{
    [TestMethod]
    public void NoReplyEndPoint()
    {
        ReplyEndPointBase endPoint = new NoReplyEndPoint();

        Assert.AreEqual(string.Empty, endPoint.Exchange, nameof(endPoint.Exchange));
        Assert.AreEqual(string.Empty, endPoint.RoutingKey, nameof(endPoint.RoutingKey));
        Assert.IsFalse(endPoint.IncludeRequestPayloadInResponse, nameof(endPoint.IncludeRequestPayloadInResponse));
    }

    [TestMethod]
    public void DirectReplyEndPoint()
    {
        ReplyEndPointBase endPoint = new DirectReplyEndPoint();

        Assert.AreEqual(string.Empty, endPoint.Exchange, nameof(endPoint.Exchange));
        Assert.AreEqual(ReplyEndPointBase.ChannelOutRoutingKey, endPoint.RoutingKey, nameof(endPoint.RoutingKey));
        Assert.IsFalse(endPoint.IncludeRequestPayloadInResponse, nameof(endPoint.IncludeRequestPayloadInResponse));
    }

    [TestMethod]
    public void QueueReplyEndPoint()
    {
        const string queueName = "MyQueue";

        ReplyEndPointBase endPoint = new QueueReplyEndPoint(queueName);

        Assert.AreEqual(string.Empty, endPoint.Exchange, nameof(endPoint.Exchange));
        Assert.AreEqual(queueName, endPoint.RoutingKey, nameof(endPoint.RoutingKey));
        Assert.IsFalse(endPoint.IncludeRequestPayloadInResponse, nameof(endPoint.IncludeRequestPayloadInResponse));
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public void QueueReplyEndPoint_Payload(bool withRequestPayload)
    {
        const string queueName = "MyQueue";

        ReplyEndPointBase endPoint = new QueueReplyEndPoint(queueName, withRequestPayload);

        Assert.AreEqual(string.Empty, endPoint.Exchange, nameof(endPoint.Exchange));
        Assert.AreEqual(queueName, endPoint.RoutingKey, nameof(endPoint.RoutingKey));
        Assert.AreEqual(withRequestPayload, endPoint.IncludeRequestPayloadInResponse, nameof(endPoint.IncludeRequestPayloadInResponse));
    }

    [DataRow("")]
    [DataRow(" ")]
    [DataRow(null)]
    [ExpectedException(typeof(ArgumentException))]
    [TestMethod]
    public void QueueReplyEndPoint_With_ArgumentException(string queueName)
    {
        ReplyEndPointBase _ = new QueueReplyEndPoint(queueName);
    }

    [TestMethod]
    public void ExchangeReplyEndPoint()
    {
        const string exchangeName = "MyExchange";

        ReplyEndPointBase endPoint = new ExchangeReplyEndPoint(exchangeName);

        Assert.AreEqual(exchangeName, endPoint.Exchange, nameof(endPoint.Exchange));
        Assert.AreEqual(string.Empty, endPoint.RoutingKey, nameof(endPoint.RoutingKey));
        Assert.IsFalse(endPoint.IncludeRequestPayloadInResponse, nameof(endPoint.IncludeRequestPayloadInResponse));
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public void ExchangeReplyEndPoint_Payload(bool withRequestPayload)
    {
        const string exchangeName = "MyExchange";

        ReplyEndPointBase endPoint = new ExchangeReplyEndPoint(exchangeName, includeRequestPayloadInResponse: withRequestPayload);

        Assert.AreEqual(exchangeName, endPoint.Exchange, nameof(endPoint.Exchange));
        Assert.AreEqual(string.Empty, endPoint.RoutingKey, nameof(endPoint.RoutingKey));
        Assert.AreEqual(withRequestPayload, endPoint.IncludeRequestPayloadInResponse, nameof(endPoint.IncludeRequestPayloadInResponse));
    }

    [TestMethod]
    public void ExchangeReplyEndPoint_With_RoutingKey()
    {
        const string exchangeName = "MyExchange";
        const string queueName = "MyQueue";

        ReplyEndPointBase endPoint = new ExchangeReplyEndPoint(exchangeName, queueName);

        Assert.AreEqual(exchangeName, endPoint.Exchange, nameof(endPoint.Exchange));
        Assert.AreEqual(queueName, endPoint.RoutingKey, nameof(endPoint.RoutingKey));
    }

    [DataRow("")]
    [DataRow(" ")]
    [DataRow(null)]
    [ExpectedException(typeof(ArgumentException))]
    [TestMethod]
    public void ExchangeReplyEndPoint_With_ArgumentException(string exchangeName)
    {
        ReplyEndPointBase _ = new ExchangeReplyEndPoint(exchangeName);
    }
}