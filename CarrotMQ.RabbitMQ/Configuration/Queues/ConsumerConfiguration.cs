using System;
using System.Collections.Generic;
using CarrotMQ.RabbitMQ.Connectivity;

namespace CarrotMQ.RabbitMQ.Configuration.Queues;

/// <summary>
/// Represents the options for configuring a queue consumer
/// </summary>
public sealed class ConsumerConfiguration
{
    /// <summary>
    /// Maximum number of concurrent messages that the broker will deliver, 0 if unlimited.
    /// (see <see href="https://www.rabbitmq.com/confirms.html#channel-qos-prefetch" />)<br />
    /// Defaults to <c>4</c>.
    /// </summary>
    public ushort PrefetchCount { get; set; } = 4;

    /// <summary>
    /// Acknowledgment count:
    /// <list type="table">
    ///     <item><c>0</c> -> autoAck</item>
    ///     <item><c>1</c> -> ack/nack for every message</item>
    ///     <item><c>>1</c> -> ack/nack for multiple messages</item>
    /// </list>
    /// <br />
    /// Defaults to <c>1</c>.
    /// </summary>
    public ushort AckCount { get; set; } = 1;

    /// <summary>
    /// Timeout of message processing. <br />
    /// Defaults to <c>5 minutes</c>.
    /// </summary>
    /// <remarks>
    /// If a message exceeds this time, the cancellation token given to the handler is set to cancelled.
    /// </remarks>
    public TimeSpan MessageProcessingTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Custom arguments that will directly be passed to the consumer (<see cref="IConsumerChannel.StartConsumingAsync" />).
    /// </summary>
    public IDictionary<string, object?> Arguments { get; set; } = new Dictionary<string, object?>();
}