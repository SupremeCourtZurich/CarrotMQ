using System.Diagnostics;
using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.Protocol;
using Microsoft.Extensions.Options;

namespace CarrotMQ.Core.Telemetry;

/// <summary>
/// Factory for creating <see cref="Activity" /> for CarrotMQ tracing
/// </summary>
public static class CarrotActivityFactory
{
    /// <summary>
    /// Key for remote service name in the activity tags.
    /// </summary>
    public const string CarrotRemoteServiceNameKey = "carrotmq.remote_service_name";

    /// <summary>
    /// Key for service name in the activity tags.
    /// </summary>
    public const string CarrotServiceNameKey = "carrotmq.service_name";

    /// <summary>
    /// Key for the VHost in the activity tags
    /// </summary>
    public const string CarrotVHostNameKey = "carrotmq.v_host";

    /// <summary>
    /// The <see cref="System.Diagnostics.ActivitySource" /> used for CarrotMQ tracing.
    /// </summary>
    private static readonly ActivitySource ActivitySource = new(Names.CarrotActivitySourceName);

    /// <summary>
    /// Creates a new Consumer tracing activity.
    /// </summary>
    /// <param name="header">The CarrotMQ header containing tracing information.</param>
    /// <param name="serviceName">The name of the service creating the activity.</param>
    /// <param name="vhost">The rabbitMq VHost to which the service creating the activity is connected.</param>
    /// <param name="carrotTracingOptions">Options for CarrotMQ tracing.</param>
    /// <returns>The created <see cref="Activity" /> if successful; otherwise, <c>null</c>.</returns>
    public static Activity? CreateConsumerActivity(
        CarrotHeader header,
        string serviceName,
        string vhost,
        IOptions<CarrotTracingOptions> carrotTracingOptions)
    {
        var parentContext = CreateParentContext(header);
        var activity = ActivitySource.StartActivity(header.CalledMethod, ActivityKind.Consumer, parentContext);

        if (activity != null)
        {
            activity.AddTag(CarrotRemoteServiceNameKey, header.ServiceName);
            activity.AddTag(CarrotServiceNameKey, serviceName);
            activity.AddTag(CarrotVHostNameKey, vhost);
            carrotTracingOptions.Value.EnrichConsumeActivityWithHeader.Invoke(activity, header);
        }

        return activity;
    }

    /// <summary>
    /// Creates a new Producer tracing activity.
    /// </summary>
    /// <param name="header">The CarrotMQ header containing tracing information.</param>
    /// <param name="serviceName">The name of the service creating the activity.</param>
    /// <param name="vhost">The rabbitMq VHost to which the service creating the activity is connected.</param>
    /// <param name="carrotTracingOptions">Options for CarrotMQ tracing.</param>
    /// <returns>The created <see cref="Activity" /> if successful; otherwise, <c>null</c>.</returns>
    public static Activity? CreateProducerActivity(
        CarrotHeader header,
        string serviceName,
        string vhost,
        IOptions<CarrotTracingOptions> carrotTracingOptions)
    {
        var activity = ActivitySource.StartActivity(header.CalledMethod, ActivityKind.Producer);

        if (activity != null)
        {
            activity.AddTag(CarrotServiceNameKey, serviceName);
            activity.AddTag(CarrotVHostNameKey, vhost);
            carrotTracingOptions.Value.EnrichPublishActivityWithHeader.Invoke(activity, header);

            header.TraceId = activity.TraceId.ToString();
            header.SpanId = activity.SpanId.ToString();
        }

        return activity;
    }

    private static ActivityContext CreateParentContext(CarrotHeader header)
    {
        if (string.IsNullOrWhiteSpace(header.TraceId))
        {
            return default;
        }

#if NET
        var activityTraceId = ActivityTraceId.CreateFromString(header.TraceId);
        var activitySpanId = !string.IsNullOrWhiteSpace(header.SpanId) ? ActivitySpanId.CreateFromString(header.SpanId) : default;
#else
        var activityTraceId = ActivityTraceId.CreateFromString(header.TraceId!.ToCharArray());
        var activitySpanId =
            !string.IsNullOrWhiteSpace(header.SpanId) ? ActivitySpanId.CreateFromString(header.SpanId!.ToCharArray()) : default;
#endif

        return new ActivityContext(activityTraceId, activitySpanId, ActivityTraceFlags.Recorded);
    }
}