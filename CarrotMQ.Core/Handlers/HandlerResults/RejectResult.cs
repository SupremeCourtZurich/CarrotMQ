using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core.Handlers.HandlerResults;

/// <summary>
/// Represents the result of a message that could not be handled.
/// When a message is rejected, it is preserved and requires developer intervention.
/// </summary>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class RejectResult : IHandlerResult
{
    /// <inheritdoc cref="RejectResult" />
    public RejectResult(CarrotError? carrotError = null)
    {
        Response = new CarrotResponse
        {
            Error = carrotError,
            StatusCode = CarrotStatusCode.InternalServerError
        };
    }

    /// <inheritdoc />
    public DeliveryStatus DeliveryStatus => DeliveryStatus.Reject;

    /// <inheritdoc />
    public CarrotResponse Response { get; }
}