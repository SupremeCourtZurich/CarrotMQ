namespace CarrotMQ.RabbitMQ.Configuration;

/// <summary>
/// Represents the options for configuring publisher confirms
/// </summary>
public sealed class PublisherConfirmOptions
{
    /// <summary>
    /// Limits number of concurrent messages with outstanding confirm
    /// </summary>
    public ushort MaxConcurrentConfirms { get; set; } = 500;

    /// <summary>
    /// Limits the number of republishes if no positive acknowledgment is received
    /// <see cref="RetryIntervalInMs" />
    /// </summary>
    public ushort RetryLimit { get; set; } = 100;

    /// <summary>
    /// Retry interval for republishing a message
    /// The actual interval between publishes can be higher depending on <see cref="RepublishEvaluationIntervalInMs" />
    /// <see cref="RetryLimit" />
    /// </summary>
    public uint RetryIntervalInMs { get; set; } = 1000;

    /// <summary>
    /// Interval between evaluations of messages with outstanding confirm on the basis of <see cref="RetryIntervalInMs" /> and
    /// <see cref="RetryLimit" />
    /// </summary>
    public uint RepublishEvaluationIntervalInMs { get; set; } = 100;

    /// <summary>
    /// Milliseconds without activity after which the channels used to republish refused messages are closed; also the
    /// longest time they wait for the reply to a request before publishing the next message.
    /// </summary>
    public uint RecoveryChannelIdleTimeoutInMs { get; set; } = 60_000;
}