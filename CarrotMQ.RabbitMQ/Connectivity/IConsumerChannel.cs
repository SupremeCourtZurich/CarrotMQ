using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CarrotMQ.Core.Common;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// Interface of a channel that consumes messages.
/// </summary>
public interface IConsumerChannel : ICarrotChannel
{
    /// <summary>
    /// Triggered when consumer is unregisted from channel
    /// </summary>
    public AsyncEventHandler<EventArgs>? UnregisteredAsync { get; set; }

    /// <summary>
    /// Triggered when consumer is registed on channel
    /// </summary>
    public AsyncEventHandler<EventArgs>? RegisteredAsync { get; set; }

    /// <summary>
    /// Starts an asynchronous consumer on the channel.
    /// </summary>
    /// <param name="queueName">The queue name to consume from.</param>
    /// <param name="ackCount">Acknowledgment count:
    /// <list type="table">
    ///     <item><c>0</c> -> autoAck</item>
    ///     <item><c>1</c> -> ack/nack for every message</item>
    ///     <item><c>>1</c> -> ack/nack for multiple messages</item>
    /// </list></param>
    /// <param name="prefetchCount">
    /// Maximum number of concurrent messages that the broker will deliver, 0 if unlimited.(see
    /// <see href="https://www.rabbitmq.com/confirms.html#channel-qos-prefetch" />).
    /// </param>
    /// <param name="consumingAsyncCallback">The async callback for incoming messages on the queue.</param>
    /// <param name="arguments">custom arguments given to the consumer.</param>
    Task StartConsumingAsync(
        string queueName,
        ushort ackCount,
        ushort prefetchCount,
        Func<CarrotMessage, Task<DeliveryStatus>> consumingAsyncCallback,
        IDictionary<string, object?>? arguments = null);

    /// <summary>
    /// Stop consuming on the channel
    /// </summary>
    Task StopConsumingAsync();

    /// <summary>
    /// Rejects a given delivery.
    /// </summary>
    /// <param name="deliveryTag">The delivery tag to reject.</param>
    /// <param name="requeue">True if delivery has to be re-queued.</param>
    Task RejectAsync(ulong deliveryTag, bool requeue);

    /// <summary>
    /// Send a basic ack on the channel for a given delivery tag.
    /// </summary>
    /// <param name="deliveryTag">The delivery tag.</param>
    /// <param name="multiple">True if multiple acknowledging is desired.</param>
    Task AckAsync(ulong deliveryTag, bool multiple);

    /// <summary>
    /// Checks if there is a consumer on the channel.
    /// </summary>
    /// <returns>True if there is a consumer on the channel. Otherwise false.</returns>
    bool HasConsumer();
}