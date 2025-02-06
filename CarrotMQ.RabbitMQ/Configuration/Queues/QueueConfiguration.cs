using System.Collections.Generic;
using RabbitMQ.Client;

namespace CarrotMQ.RabbitMQ.Configuration.Queues;

/// <summary>
/// Represents the options for configuring a queue consumer
/// </summary>
public sealed class QueueConfiguration
{
    /// <summary>
    /// Name of the queue
    /// </summary>
    public string QueueName { get; set; } = string.Empty;

    /// <summary>
    /// Flag that determines whether the queue will survive a broker restart.<br />
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>
    /// Flag that determines whether the queue is used by only one connection and will be deleted when that connection closes<br />
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool Exclusive { get; set; }

    /// <summary>
    /// Flag that determines whether the queue is deleted when last consumer unsubscribes<br />
    /// Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>Queue must at least have had one consumer for it to be auto-deleted</remarks>
    public bool AutoDelete { get; set; }

    /// <summary>
    /// Custom arguments that will directly be passed to the queue declaration (<see cref="IChannel.QueueDeclareAsync" />).
    /// </summary>
    public IDictionary<string, object?> Arguments { get; set; } = new Dictionary<string, object?>();

    /// <summary>
    /// Flag that determines whether the queue is created on start-up.<br />
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool DeclareQueue { get; set; }

    /// <summary>
    /// Flag that determines whether a consumer is created and started for this queue.<br />
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public ConsumerConfiguration? ConsumerConfiguration { get; set; }

    /// <summary>
    /// Known argument names used as keys for <see cref="Arguments" />
    /// </summary>
    public static class QueueArgumentNames
    {
        /// 
        public const string QueueType = "x-queue-type";
        /// 
        public const string SingleActiveConsumer = "x-single-active-consumer";
        /// 
        public const string DeadLetterExchange = "x-dead-letter-exchange";
        /// 
        public const string DeliveryLimit = "x-delivery-limit";
    }
}