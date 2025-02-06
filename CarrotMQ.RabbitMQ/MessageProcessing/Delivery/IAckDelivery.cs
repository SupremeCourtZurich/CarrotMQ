using System;
using System.Threading.Tasks;
using CarrotMQ.Core.MessageProcessing.Delivery;

namespace CarrotMQ.RabbitMQ.MessageProcessing.Delivery;

/// <summary>
/// Represents a delivery mechanism for acknowledging message processing status.
/// </summary>
public interface IAckDelivery : IDisposable
{
    /// <summary>
    /// Delivers the acknowledgment status for a message with the specified delivery tag.
    /// </summary>
    /// <param name="deliveryTag">The unique identifier associated with the delivered message.</param>
    /// <param name="deliveryStatus">The status indicating the result of the message processing.</param>
    Task DeliverAsync(ulong deliveryTag, DeliveryStatus deliveryStatus);
}