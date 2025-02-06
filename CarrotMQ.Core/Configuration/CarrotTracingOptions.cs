using System;
using System.Diagnostics;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core.Configuration;

/// <summary>
/// Represents the options for configuring <see cref="Activity" /> tracing for messages published and received over
/// CarrotMQ.RabbitMQ
/// </summary>
public sealed class CarrotTracingOptions
{
    /// <summary>
    /// Default section name of this option in configuration
    /// </summary>
    public const string CarrotTracing = nameof(CarrotTracing);

    private static readonly Action<Activity, CarrotHeader> DoNothing = (_, _) =>
    {
        /* do nothing */
    };

    /// <summary>
    /// Action to enrich the publish activity with custom content from CarrotMQ headers.
    /// </summary>
    public Action<Activity, CarrotHeader> EnrichPublishActivityWithHeader { get; set; } = DoNothing;

    /// <summary>
    /// Action to enrich the consume activity with custom content from CarrotMQ headers.
    /// </summary>
    public Action<Activity, CarrotHeader> EnrichConsumeActivityWithHeader { get; set; } = DoNothing;
}