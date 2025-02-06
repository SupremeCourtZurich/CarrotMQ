using System.Collections.Generic;
using CarrotMQ.Core.EndPoints;
using Microsoft.Extensions.Options;

namespace CarrotMQ.RabbitMQ.Configuration.Queues;

/// 
public class QueueCollection
{
    private readonly List<QueueConfiguration> _queueConfigurations = [];

    ///
    public QueueCollection()
    {
    }

    /// <summary>
    /// Gets a list of queue configurations in the configuration.
    /// </summary>
    /// <returns>A list of queue configurations.</returns>
    internal List<QueueConfiguration> GetQueueConfigurations()
    {
        return _queueConfigurations;
    }

    /// <summary>
    /// Adds a queue configuration to the configuration.
    /// </summary>
    /// <param name="queueConfig">The queue configuration to add.</param>
    internal void AddQueueConfiguration(QueueConfiguration queueConfig)
    {
        _queueConfigurations.Add(queueConfig);
    }

    /// <summary>
    /// Validate the configurations
    /// </summary>
    /// <exception cref="OptionsValidationException"></exception>
    public void Validate()
    {
        List<string> errors = new();
        foreach (var queueConfig in _queueConfigurations)
        {
            var consumerConfig = queueConfig.ConsumerConfiguration;

            if (consumerConfig == null) continue;

            if (consumerConfig.PrefetchCount > 0 && consumerConfig.AckCount > consumerConfig.PrefetchCount)
            {
                errors.Add(
                    $"Queue {queueConfig.QueueName}: if PrefetchCount > 0 then AckCount must be <= PrefetchCount ({consumerConfig.AckCount} <= {consumerConfig.PrefetchCount})");
            }
        }

        if (errors.Count != 0)
        {
            throw new OptionsValidationException(nameof(QueueConfiguration), typeof(QueueConfiguration), errors);
        }
    }

    /// <summary>
    /// Adds a quorum queue to the service configuration.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <returns>The <see cref="QuorumQueueBuilder" /> for additional configuration.</returns>
    /// <remarks>This method creates the queue if it does not exist.</remarks>
    public QuorumQueueBuilder AddQuorum(string queueName) => AddQuorumQueueInternal(queueName);

    /// <summary>
    /// Adds a quorum queue to the service configuration.
    /// </summary>
    /// <typeparam name="TQueueEndPoint">The type of the queue endpoint used to determine the queue name.</typeparam>
    /// <returns>The <see cref="QuorumQueueBuilder" /> for additional configuration.</returns>
    /// <remarks>
    /// This method creates the queue if it does not exist.
    /// It uses the provided type <typeparamref name="TQueueEndPoint" /> to determine the queue name.
    /// The RabbitMQ quorum queue is a modern queue type, which implements a durable, replicated FIFO queue based on the Raft
    /// consensus algorithm.
    /// Quorum queues are designed to be safer and provide simpler, well defined failure handling semantics that users should
    /// find easier to reason about when designing and operating their systems.
    /// </remarks>
    public QuorumQueueBuilder AddQuorum<TQueueEndPoint>()
        where TQueueEndPoint : QueueEndPoint, new() => AddQuorumQueueInternal(new TQueueEndPoint().QueueName);

    /// <summary>
    /// Adds a classic queue to the service configuration.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <returns>The <see cref="ClassicQueueBuilder" /> for additional configuration.</returns>
    /// <remarks>
    /// This method creates the queue if it does not exist.
    /// The RabbitMQ quorum queue is a modern queue type, which implements a durable, replicated FIFO queue based on the Raft
    /// consensus algorithm.
    /// Quorum queues are designed to be safer and provide simpler, well defined failure handling semantics that users should
    /// find easier to reason about when designing and operating their systems.
    /// </remarks>
    public ClassicQueueBuilder AddClassic(string queueName) => AddClassicQueueInternal(queueName);

    /// <summary>
    /// Adds a classic queue to the service configuration.
    /// </summary>
    /// <typeparam name="TQueueEndPoint">The type of the queue endpoint used to determine the queue name.</typeparam>
    /// <returns>The <see cref="ClassicQueueBuilder" /> for additional configuration.</returns>
    /// <remarks>
    /// This method creates the queue if it does not exist.
    /// It uses the provided type <typeparamref name="TQueueEndPoint" /> to determine the queue name.
    /// A RabbitMQ classic queue (the original queue type) is a versatile queue type suitable for use cases where data
    /// safety is not a priority because the data stored in classic queues is not replicated. Classic queues uses the
    /// non-replicated FIFO queue implementation.
    /// </remarks>
    public ClassicQueueBuilder AddClassic<TQueueEndPoint>()
        where TQueueEndPoint : QueueEndPoint, new() => AddClassicQueueInternal(new TQueueEndPoint().QueueName);

    /// <summary>
    /// Adds a queue to the service configuration.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <remarks>This method creates the queue if it does not exist. It does NOT add a consumer.</remarks>
    public Queue UseQueue(string queueName) => UseQueueInternal(queueName);

    /// <summary>
    /// Use an existing queue.
    /// </summary>
    /// <typeparam name="TQueueEndPoint">The type of the queue endpoint used to determine the queue name.</typeparam>
    /// <remarks>
    /// This method creates the queue if it does not exist. It does NOT add a consumer.
    /// It uses the provided type <typeparamref name="TQueueEndPoint" /> to determine the queue name.
    /// A RabbitMQ classic queue (the original queue type) is a versatile queue type suitable for use cases where data
    /// safety is not a priority because the data stored in classic queues is not replicated. Classic queues uses the
    /// non-replicated FIFO queue implementation.
    /// </remarks>
    public Queue UseQueue<TQueueEndPoint>()
        where TQueueEndPoint : QueueEndPoint, new() => UseQueue(new TQueueEndPoint().QueueName);

    private QuorumQueueBuilder AddQuorumQueueInternal(string queueName)
    {
        var queueConfig = new QueueConfiguration
        {
            QueueName = queueName,
            DeclareQueue = true,
            Arguments = { [QueueConfiguration.QueueArgumentNames.QueueType] = "quorum" }
        };

        AddQueueConfiguration(queueConfig);

        return new QuorumQueueBuilder(queueConfig);
    }

    private ClassicQueueBuilder AddClassicQueueInternal(string queueName)
    {
        var queueConfig = new QueueConfiguration
        {
            QueueName = queueName,
            DeclareQueue = true,
            Arguments = { [QueueConfiguration.QueueArgumentNames.QueueType] = "classic" }
        };

        AddQueueConfiguration(queueConfig);

        return new ClassicQueueBuilder(queueConfig);
    }

    private Queue UseQueueInternal(string queueName)
    {
        var queueConfig = new QueueConfiguration
        {
            QueueName = queueName,
            DeclareQueue = false
        };

        AddQueueConfiguration(queueConfig);

        return new Queue(queueConfig);
    }
}