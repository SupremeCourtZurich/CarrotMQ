using System;
using System.Collections.Generic;

namespace CarrotMQ.Core.Handlers;

/// <summary>
/// Contains information from the <see cref="Context" /> sent by the client and some additional information about the
/// received message in the consumer context.
/// </summary>
public sealed class ConsumerContext : Context
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConsumerContext" /> class with the specified parameters.
    /// </summary>
    /// <param name="initialUserName">See <see cref="Context.InitialUserName" />.</param>
    /// <param name="initialServiceName">See <see cref="Context.InitialServiceName" />.</param>
    /// <param name="customHeader">See <see cref="Context.CustomHeader" />.</param>
    /// <param name="messageProperties">See <see cref="MessageProperties" />.</param>
    /// <param name="messageId">See <see cref="MessageId" />.</param>
    /// <param name="correlationId">See <see cref="CorrelationId" />.</param>
    /// <param name="createdAt">See <see cref="CreatedAt" />.</param>
    public ConsumerContext(
        string? initialUserName,
        string? initialServiceName,
        MessageProperties messageProperties,
        IDictionary<string, string>? customHeader,
        Guid messageId,
        Guid? correlationId,
        DateTimeOffset createdAt) : base(initialUserName, initialServiceName, customHeader)
    {
        MessageProperties = messageProperties;
        MessageId = messageId;
        CorrelationId = correlationId;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Gets the message properties. The default is <see cref="MessageProperties.Default" />
    /// </summary>
    public MessageProperties MessageProperties { get; }

    /// <summary>
    /// Gets the unique message id.
    /// </summary>
    public Guid MessageId { get; }

    /// <summary>
    /// Gets the id which correlates a response to the original request.
    /// </summary>
    public Guid? CorrelationId { get; }

    /// <summary>
    /// Gets the message creation time.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }
}