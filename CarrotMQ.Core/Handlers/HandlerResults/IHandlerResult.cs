using CarrotMQ.Core.MessageProcessing.Delivery;

namespace CarrotMQ.Core.Handlers.HandlerResults;

/// <summary>
/// Represents the result of handling a message.
/// </summary>
public interface IHandlerResult
{
    /// 
    public DeliveryStatus DeliveryStatus { get; }

    /// 
    public CarrotResponse Response { get; }
}