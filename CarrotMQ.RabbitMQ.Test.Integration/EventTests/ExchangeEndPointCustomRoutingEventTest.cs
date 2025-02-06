using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.EventTests;

/// <summary>
/// Send event to ExchangeEndPoint
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class ExchangeEndPointCustomRoutingEventTest : TestBaseNoReply
{
    [TestMethod]
    public async Task ExchangeEndPoint_CustomRoutingEvent_OK()
    {
        const int id = 2001;

        await CarrotClient.PublishAsync(
            new ExchangeEndPointCustomRoutingEvent(
                TestExchange.Name,
                ExchangeEndPointCustomRoutingEvent.GetRoutingKey(),
                id));

        await VerifyOk(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_CustomRoutingEvent_Exception()
    {
        const int id = 2002;

        await CarrotClient.PublishAsync(
            new ExchangeEndPointCustomRoutingEvent(
                TestExchange.Name,
                ExchangeEndPointCustomRoutingEvent.GetRoutingKey(),
                id) { ThrowException = true });

        await VerifyException(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_CustomRoutingEvent_Reject()
    {
        const int id = 2007;

        await CarrotClient.PublishAsync(
            new ExchangeEndPointCustomRoutingEvent(
                TestExchange.Name,
                ExchangeEndPointCustomRoutingEvent.GetRoutingKey(),
                id) { DoReject = true });

        await VerifyDoReject(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_CustomRoutingEvent_RetryDeadLetter()
    {
        const int id = 2008;

        await CarrotClient.PublishAsync(
            new ExchangeEndPointCustomRoutingEvent(
                TestExchange.Name,
                ExchangeEndPointCustomRoutingEvent.GetRoutingKey(),
                id) { DoRetry = true });

        await VerifyDoRetry(id);
    }
}