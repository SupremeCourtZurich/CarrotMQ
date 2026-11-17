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
    /// If <c>true</c>, a publish the broker refuses (for example to a missing exchange) fails at once with its
    /// <see cref="Core.Protocol.PublishFailedException.Reason" />, and the other unconfirmed messages are republished one
    /// at a time on a separate channel. If <c>false</c>, the refused message is retried like any unconfirmed message
    /// until <see cref="RetryLimit" /> is exceeded; each attempt closes the channel, so messages published after it are
    /// retried with it and can exceed <see cref="RetryLimit" /> too.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool IsolateRefusedPublishes { get; set; }

    /// <summary>
    /// Milliseconds without activity after which the channel used to republish refused messages is closed; also the
    /// longest time it waits for the reply to a request before publishing the next message.
    /// Only used with <see cref="IsolateRefusedPublishes" />.
    /// </summary>
    public uint RecoveryChannelIdleTimeoutInMs { get; set; } = 60_000;
}