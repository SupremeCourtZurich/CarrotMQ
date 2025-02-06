namespace CarrotMQ.RabbitMQ.Configuration.Queues;

/// <summary>
/// Configuration builder for classic queues
/// </summary>
public class ClassicQueueBuilder : QueueBuilder<ClassicQueueBuilder>
{
    internal ClassicQueueBuilder(QueueConfiguration queueConfiguration) : base(queueConfiguration)
    {
    }
}