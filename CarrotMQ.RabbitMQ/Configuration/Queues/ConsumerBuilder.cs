using System;

namespace CarrotMQ.RabbitMQ.Configuration.Queues;

/// 
public class ConsumerBuilder
{
    private readonly ConsumerConfiguration _consumerConfiguration;

    internal ConsumerBuilder(QueueConfiguration queueConfiguration)
    {
        _consumerConfiguration = new ConsumerConfiguration();
        queueConfiguration.ConsumerConfiguration = _consumerConfiguration;
    }

    /// <summary>
    /// Sets the acknowledgment mode for the queue to auto-ack (sets the <see cref="ConsumerConfiguration.AckCount" /> = 0).
    /// </summary>
    public ConsumerBuilder WithAutoAck()
    {
        return WithAckCount(0);
    }

    /// <summary>
    /// Sets the acknowledgment mode for the queue to single-ack (sets the <see cref="ConsumerConfiguration.AckCount" /> = 1).
    /// </summary>
    public ConsumerBuilder WithSingleAck()
    {
        return WithAckCount(1);
    }

    /// <inheritdoc cref="ConsumerConfiguration.AckCount" />
    public ConsumerBuilder WithAckCount(ushort ackCount)
    {
        _consumerConfiguration.AckCount = ackCount;

        return this;
    }

    /// <inheritdoc cref="ConsumerConfiguration.PrefetchCount" />
    public ConsumerBuilder WithPrefetchCount(ushort prefetchCount)
    {
        _consumerConfiguration.PrefetchCount = prefetchCount;

        return this;
    }

    /// <summary>
    /// Sets a custom argument for the consumer.
    /// </summary>
    /// <param name="key">The key of the custom argument.</param>
    /// <param name="value">The value of the custom argument.</param>
    public ConsumerBuilder WithCustomArgument(string key, object value)
    {
        _consumerConfiguration.Arguments[key] = value;

        return this;
    }

    /// <inheritdoc cref="ConsumerConfiguration.MessageProcessingTimeout" />
    public ConsumerBuilder WithMessageProcessingTimeout(TimeSpan messageProcessingTimeout)
    {
        _consumerConfiguration.MessageProcessingTimeout = messageProcessingTimeout;

        return this;
    }
}