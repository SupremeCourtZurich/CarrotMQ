namespace CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

public sealed class BarrierBag
{
    public IDictionary<Guid, AsyncBarrier> Barriers = new Dictionary<Guid, AsyncBarrier>();
}