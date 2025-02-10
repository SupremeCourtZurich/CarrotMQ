using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using CarrotMQ.Core.MessageProcessing.Delivery;
#if NET
using System.Diagnostics;
#endif

namespace CarrotMQ.Core.Telemetry;

/// <summary>
/// Class for recording metrics related to CarrotMQ processing, such as request duration and message delivery status.
/// </summary>
internal sealed class CarrotMetricsRecorder : ICarrotMetricsRecorder
{
    /// <summary>
    /// Counter to track the number of active requests.
    /// </summary>
    private readonly UpDownCounter<long> _activeRequestsCounter;

    /// <summary>
    /// Counter to track message delivery.
    /// </summary>
    private readonly Counter<long> _messageDeliveryCounter;

    /// <summary>
    /// Meter to aggregate and expose metrics.
    /// </summary>
    private readonly Meter _meter;

    /// <summary>
    /// Histogram to record the distribution of request durations.
    /// </summary>
    private readonly Histogram<double> _requestDurationHistogram;

    /// <summary>
    /// Counter to track the number of requests.
    /// </summary>
    private readonly Counter<long> _requestsCounter;

    /// <summary>
    /// Counter to track response statuses.
    /// </summary>
    private readonly Counter<long> _responseStatusCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="CarrotMetricsRecorder" /> class.
    /// </summary>
    /// <param name="meterFactory">Factory for creating meters.</param>
    public CarrotMetricsRecorder(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(Names.CarrotMeterName);

        _requestDurationHistogram = _meter.CreateHistogram<double>("carrotmq-request-duration-ms");
        _activeRequestsCounter = _meter.CreateUpDownCounter<long>("carrotmq-active-requests-counter");
        _requestsCounter = _meter.CreateCounter<long>("carrotmq-request-counter");
        _responseStatusCounter = _meter.CreateCounter<long>("carrotmq-response-counter");
        _messageDeliveryCounter = _meter.CreateCounter<long>("carrotmq-message-delivery-counter");
    }

    /// <inheritdoc />
    public long StartConsuming()
    {
        _activeRequestsCounter.Add(1);
#if NET
        return Stopwatch.GetTimestamp();
#else
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
#endif
    }

    /// <inheritdoc />
    public void RecordMessageType(string calledMethod)
    {
        _requestsCounter.Add(1, new KeyValuePair<string, object?>("CalledMethod", calledMethod));
    }

    /// <inheritdoc />
    public void ResponsePublished(string calledMethod, int statusCode)
    {
        var labels = new[]
        {
            new KeyValuePair<string, object?>("CalledMethod", calledMethod), new KeyValuePair<string, object?>("StatusCode", statusCode)
        };

        _responseStatusCounter.Add(1, labels);
    }

    /// <inheritdoc />
    public void EndConsuming(long timestamp, DeliveryStatus deliveryStatus)
    {
#if NET
        TimeSpan duration = Stopwatch.GetElapsedTime(timestamp);
#else
        long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        TimeSpan duration = TimeSpan.FromMilliseconds(currentTimestamp - timestamp);
#endif
        _activeRequestsCounter.Add(-1);
        _requestDurationHistogram.Record(duration.TotalMilliseconds);
        _messageDeliveryCounter.Add(1, new KeyValuePair<string, object?>("DeliveryStatus", deliveryStatus.ToString()));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _meter.Dispose();
    }
}