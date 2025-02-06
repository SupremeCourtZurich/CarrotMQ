using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core.Handlers.HandlerResults;

/// <summary>
/// Represents the result of handler that encountered a transient issue.
/// The message is put back on the queue to be retried later.
/// </summary>
/// <remarks>It is recommended to delay before returning a retry.</remarks>
public class RetryResult : IHandlerResult
{
    /// <inheritdoc cref="RetryResult" />
    public RetryResult(CarrotError? error = null)
    {
        Response = new CarrotResponse
        {
            Error = error,
            StatusCode = CarrotStatusCode.InternalServerError
        };
    }

    /// <inheritdoc />
    public DeliveryStatus DeliveryStatus => DeliveryStatus.Retry;

    /// <inheritdoc />
    public CarrotResponse Response { get; }
}