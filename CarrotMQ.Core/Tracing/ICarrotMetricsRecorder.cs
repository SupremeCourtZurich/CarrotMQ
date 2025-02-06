using System;
using CarrotMQ.Core.MessageProcessing.Delivery;

namespace CarrotMQ.Core.Tracing;

/// <summary>
/// Interface for recording metrics related to CarrotMQ processing, such as request duration and message delivery status.
/// </summary>
public interface ICarrotMetricsRecorder : IDisposable
{
    /// <summary>
    /// Records the metrics for the start of message consuming.
    /// </summary>
    /// <returns>The current timestamp when the message is received (in ticks or UnixTimeMilliseconds depending on the current .NET version).</returns>
    long StartConsuming();

    /// <summary>
    /// Records the metric to count messages based on calledMethod
    /// </summary>
    /// <param name="calledMethod"></param>
    void RecordMessageType(string calledMethod);

    /// <summary>
    /// Record metric for response publishing (CalledMethod and StatusCode)
    /// </summary>
    /// <param name="calledMethod">CalledMethod of the response message (Response:RequestCalledMethod)</param>
    /// <param name="statusCode">Status code of the response</param>
    void ResponsePublished(string calledMethod, int statusCode);

    /// <summary>
    /// Records the metrics of the end of message consuming. Captures the duration and delivery status.
    /// </summary>
    /// <param name="timestamp">
    /// The timestamp when the incoming message was received (in ticks or UnixTimeMilliseconds depending on the current .NET
    /// version).
    /// </param>
    /// <param name="deliveryStatus">The status of the message delivery.</param>
    void EndConsuming(long timestamp, DeliveryStatus deliveryStatus);
}