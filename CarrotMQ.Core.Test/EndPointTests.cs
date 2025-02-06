using CarrotMQ.Core.EndPoints;
using CarrotMQ.Core.MessageProcessing;
using NSubstitute;

namespace CarrotMQ.Core.Test;

[TestClass]
public sealed class EndPointTests
{
    private IRoutingKeyResolver _resolver = default!;

    [TestInitialize]
    public void Initialize()
    {
        _resolver = Substitute.For<IRoutingKeyResolver>();
    }

    [TestMethod]
    public void QueueEndPoint()
    {
        const string queueName = "MyQueue";

        EndPointBase endPoint = new TestQueueEndPoint(queueName);

        Assert.AreEqual(string.Empty, endPoint.Exchange, nameof(endPoint.Exchange));
        Assert.AreEqual(queueName, endPoint.GetRoutingKey<object>(_resolver), nameof(endPoint.GetRoutingKey));
    }

    [DataRow("")]
    [DataRow(" ")]
    [DataRow(null)]
    [ExpectedException(typeof(ArgumentException))]
    [TestMethod]
    public void QueueEndPoint_With_ArgumentException(string queueName)
    {
        EndPointBase _ = new TestQueueEndPoint(queueName);
    }

    [TestMethod]
    public void ExchangeEndPoint()
    {
        const string routingKey = "MyRoutingKey";
        const string exchangeName = "MyExchange";
        _resolver.GetRoutingKey<object>(Arg.Is<string>(x => x == exchangeName)).ReturnsForAnyArgs(_ => routingKey);

        EndPointBase endPoint = new TestExchangeEndPoint(exchangeName);

        Assert.AreEqual(exchangeName, endPoint.Exchange, nameof(endPoint.Exchange));
        Assert.AreEqual(routingKey, endPoint.GetRoutingKey<object>(_resolver), nameof(endPoint.GetRoutingKey));
    }

    [DataRow("")]
    [DataRow(" ")]
    [DataRow(null)]
    [ExpectedException(typeof(ArgumentException))]
    [TestMethod]
    public void ExchangeEndPoint_With_ArgumentException(string exchangeName)
    {
        EndPointBase _ = new TestExchangeEndPoint(exchangeName);
    }

    public sealed class TestQueueEndPoint : QueueEndPoint
    {
        public TestQueueEndPoint(string queueName) : base(queueName)
        {
        }
    }

    public sealed class TestExchangeEndPoint : ExchangeEndPoint
    {
        public TestExchangeEndPoint(string exchangeName) : base(exchangeName)
        {
        }
    }
}