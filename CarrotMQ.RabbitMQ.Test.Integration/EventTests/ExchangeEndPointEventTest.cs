using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.EventTests;

/// <summary>
/// Send event to ExchangeEndPoint
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class ExchangeEndPointEventTest : TestBaseNoReply
{
    [TestMethod]
    public async Task ExchangeEndPoint_Event_OK()
    {
        const int id = 2101;

        await CarrotClient.PublishAsync(new ExchangeEndPointEvent(id));

        await VerifyOk(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_Event_Exception()
    {
        const int id = 2102;

        await CarrotClient.PublishAsync(new ExchangeEndPointEvent(id) { ThrowException = true });

        await VerifyException(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_Event_Reject()
    {
        const int id = 2107;

        await CarrotClient.PublishAsync(new ExchangeEndPointEvent(id) { DoReject = true });

        await VerifyDoReject(id);
    }

    [TestMethod]
    public async Task ExchangeEndPoint_Event_RetryDeadLetter()
    {
        const int id = 2108;

        await CarrotClient.PublishAsync(new ExchangeEndPointEvent(id) { DoRetry = true });

        await VerifyDoRetry(id);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task ExchangeEndPoint_Event_Load_OK()
    {
        const int startId = 2110;
        const int eventCount = 20;
        Guid barrierId = Guid.NewGuid();
        var barrier = new AsyncBarrier(eventCount + 1);
        BarrierBag.Barriers.Add(barrierId, barrier);

        for (int i = startId; i < startId + eventCount; i++)
        {
            await CarrotClient.PublishAsync(new ExchangeEndPointEvent(i)
            {
                BarrierId = barrierId
            });
        }

        await barrier.SignalAndWaitAsync();

        await VerifyOk(startId, eventCount);
    }
}