namespace CarrotMQ.RabbitMQ.Configuration.Queues;

/// <summary>
/// Configuration builder for quorum queues
/// </summary>
public class QuorumQueueBuilder : QueueBuilder<QuorumQueueBuilder>
{
    internal QuorumQueueBuilder(QueueConfiguration queueConfiguration) : base(queueConfiguration)
    {
    }

    /// <summary>
    /// Sets the delivery limit for the queue (max retries for quorum queues).
    /// </summary>
    public QuorumQueueBuilder WithDeliveryLimit(uint deliveryLimit)
    {
        QueueConfiguration.Arguments[QueueConfiguration.QueueArgumentNames.DeliveryLimit] = deliveryLimit;

        return this;
    }
}