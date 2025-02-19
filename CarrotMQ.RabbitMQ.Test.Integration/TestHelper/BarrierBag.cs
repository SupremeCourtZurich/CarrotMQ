namespace CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

public sealed class BarrierBag
{
    public IDictionary<Guid, Barrier> Barriers = new Dictionary<Guid, Barrier>();
}