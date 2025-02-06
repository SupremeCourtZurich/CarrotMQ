using System;

namespace CarrotMQ.RabbitMQ.Configuration.Queues;

/// 
public class Queue
{
    private protected readonly QueueConfiguration QueueConfiguration;

    internal Queue(QueueConfiguration queueConfiguration)
    {
        QueueConfiguration = queueConfiguration;
    }

    internal string QueueName => QueueConfiguration.QueueName;

    /// <summary>
    /// Uses a consumer for this queue
    /// </summary>
    public Queue WithConsumer(Action<ConsumerBuilder>? consumerConfiguration = null)
    {
        consumerConfiguration?.Invoke(new ConsumerBuilder(QueueConfiguration));

        return this;
    }
}

/// 
public class Queue<TQueue> : Queue
    where TQueue : Queue<TQueue>
{
    internal Queue(QueueConfiguration queueConfiguration) : base(queueConfiguration)
    {
    }

    /// <summary>
    /// Uses a consumer for this queue
    /// </summary>
    public new TQueue WithConsumer(Action<ConsumerBuilder>? consumerConfiguration = null)
    {
        consumerConfiguration?.Invoke(new ConsumerBuilder(QueueConfiguration));

        return (TQueue)this;
    }
}