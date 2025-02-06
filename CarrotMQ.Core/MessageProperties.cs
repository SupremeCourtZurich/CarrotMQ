namespace CarrotMQ.Core;

/// <summary>
/// Contains AMQP-properties
/// </summary>
public record struct MessageProperties
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessageProperties" /> struct.
    /// </summary>
    public MessageProperties()
    {
    }

    /// <summary>
    /// Default MessageProperties using PublisherConfirm
    /// </summary>
    public static MessageProperties Default => new() { PublisherConfirm = true };

    /// <summary>
    /// Gets or sets the flag whether to use publisher confirm for this message
    /// </summary>
    public bool PublisherConfirm { get; set; } = true;

    /// <summary>
    /// Gets or sets the flag whether the message will be persisted on disk (rabbitMq)
    /// </summary>
    /// <remarks>only relevant when using classic queues (on a quorum queue messages are always persistent)</remarks>
    public bool Persistent { get; set; } = false;

    /// <summary>
    /// Gets or sets the message priority, 0 to 9.
    /// </summary>
    public byte Priority { get; set; } = default;

    /// <summary>
    /// Gets or sets the Time To Live (TTL) in milliseconds of CarrotMQ messages.<br />
    /// Timeout while ...
    /// <list type="bullet">
    ///     <item>
    ///         <description>publishing on the publisher side</description>
    ///     </item>
    ///     <item>
    ///         <description>waiting in the queue. AMQP expiration: Message expiration specification.</description>
    ///     </item>
    ///     <item>
    ///         <description>handling the request on the consumer side</description>
    ///     </item>
    /// </list>
    /// </summary>
    public int? Ttl { get; set; }
}