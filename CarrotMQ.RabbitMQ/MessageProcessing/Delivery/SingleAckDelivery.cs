using System;
using System.Threading.Tasks;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.RabbitMQ.Connectivity;

namespace CarrotMQ.RabbitMQ.MessageProcessing.Delivery;

/// <summary>
/// Provides a mechanism for delivering single acknowledgments or rejections for individual messages.
/// </summary>
internal sealed class SingleAckDelivery : IAckDelivery
{
    private readonly IConsumerChannel _channel;

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleAckDelivery" /> class with the specified consumer channel.
    /// </summary>
    /// <param name="channel">The consumer channel used for acknowledging or rejecting messages.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="channel" /> is null.</exception>
    public SingleAckDelivery(IConsumerChannel channel)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
    }

    /// <summary>
    /// Delivers the acknowledgment or rejection for an individual message based on the specified delivery status.
    /// </summary>
    /// <param name="deliveryTag">The delivery tag of the message.</param>
    /// <param name="deliveryStatus">The delivery status indicating whether to acknowledge, reject, or retry the message.</param>
    public async Task DeliverAsync(ulong deliveryTag, DeliveryStatus deliveryStatus)
    {
        switch (deliveryStatus)
        {
            case DeliveryStatus.Retry:
                await _channel.RejectAsync(deliveryTag, requeue: true).ConfigureAwait(false);

                break;
            case DeliveryStatus.Reject:
                await _channel.RejectAsync(deliveryTag, requeue: false).ConfigureAwait(false);

                break;
            case DeliveryStatus.Ack:
                await _channel.AckAsync(deliveryTag, multiple: false).ConfigureAwait(false);

                break;
            default:
                throw new ArgumentException($"{deliveryStatus} not supported!", nameof(deliveryStatus));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Nothing to dispose
    }
}