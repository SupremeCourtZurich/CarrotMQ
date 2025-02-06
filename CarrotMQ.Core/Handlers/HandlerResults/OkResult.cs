using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core.Handlers.HandlerResults;

/// <summary>
/// Represents the result of a successfully handled message.
/// </summary>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class OkResult : IHandlerResult
{
    /// <inheritdoc cref="OkResult" />
    public OkResult(object? response = null)
    {
        Response = new CarrotResponse
        {
            Content = response,
            StatusCode = CarrotStatusCode.Ok
        };
    }

    /// <inheritdoc />
    public DeliveryStatus DeliveryStatus => DeliveryStatus.Ack;

    /// <inheritdoc />
    public CarrotResponse Response { get; }
}