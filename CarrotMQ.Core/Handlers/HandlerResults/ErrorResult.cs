using CarrotMQ.Core.MessageProcessing.Delivery;

namespace CarrotMQ.Core.Handlers.HandlerResults;

/// <summary>
/// Represents that an error occured during a request that expects a response.
/// The message will not be retried and the response contains information
/// about the error (e.g. the message represented an invalid command)
/// </summary>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class ErrorResult : IHandlerResult
{
    /// <inheritdoc cref="ErrorResult" />
    public ErrorResult(CarrotError? error, object? response, int statusCode = 500)
    {
        Response = new CarrotResponse
        {
            Content = response,
            Error = error,
            StatusCode = statusCode
        };
    }

    /// <inheritdoc />
    public DeliveryStatus DeliveryStatus => DeliveryStatus.Ack;

    /// <inheritdoc />
    public CarrotResponse Response { get; }
}