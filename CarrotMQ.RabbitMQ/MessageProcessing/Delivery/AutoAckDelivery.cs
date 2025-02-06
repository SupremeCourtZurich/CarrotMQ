using System.Threading.Tasks;
using CarrotMQ.Core.MessageProcessing.Delivery;
using Microsoft.Extensions.Logging;

namespace CarrotMQ.RabbitMQ.MessageProcessing.Delivery;

/// <summary>
/// Represents an implementation of the <see cref="IAckDelivery" /> interface that automatically acknowledges message
/// delivery without any further action.
/// </summary>
internal sealed class AutoAckDelivery : IAckDelivery
{
    private readonly ILogger _logger;

    public AutoAckDelivery(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task DeliverAsync(ulong deliveryTag, DeliveryStatus deliveryStatus)
    {
        switch (deliveryStatus)
        {
            case DeliveryStatus.Reject:
                _logger.LogWarning("Message with deliveryTag {DeliveryTag} can not be rejected in AutoAck mode", deliveryTag.ToString());

                break;
            case DeliveryStatus.Retry:
                _logger.LogError("Message with deliveryTag {DeliveryTag} can not be retried in AutoAck mode", deliveryTag.ToString());

                break;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}