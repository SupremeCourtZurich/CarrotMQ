using System;
using System.Collections.Generic;

namespace CarrotMQ.Core.Protocol;

/// <summary>
/// Represents the headers associated with a CarrotMQ message.
/// </summary>
public sealed class CarrotHeader
{
    /// <summary>
    /// AMQP correlation-id: Application correlation identifier.
    /// </summary>
    public Guid? CorrelationId { get; set; }

    /// <summary>
    /// Name of the user sending the initial message.<br />
    /// Set with <see cref="Context.InitialUserName" />.
    /// </summary>
    public string? InitialUserName { get; set; }

    /// <summary>
    /// Name of the service or application sending the initial message.<br />
    /// Set with <see cref="Context.InitialServiceName" />
    /// </summary>
    public string? InitialServiceName { get; set; }

    /// <summary>
    /// Name of the service or application sending the message.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Unique ID identifying the service instance sending the message.
    /// </summary>
    public Guid ServiceInstanceId { get; set; }

    /// <summary>
    /// AMQP message-id: Application message identifier.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// OpenTelemetry trace id. Format is based on a W3C standard.
    /// </summary>
    public string? TraceId { get; set; }

    /// <summary>
    /// OpenTelemetry span id. Format is based on a W3C standard.
    /// </summary>
    public string? SpanId { get; set; }

    /// <summary>
    /// Timestamp indicating when the message was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>
    /// Exchange where the message is sent to.
    /// </summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>
    /// Routing key for this message.
    /// </summary>
    public string RoutingKey { get; set; } = string.Empty;

    /// <summary>
    /// Called method for this message (used to determine which handler will be executed when receiving the message).
    /// </summary>
    public string CalledMethod { get; set; } = string.Empty;

    /// <summary>
    /// Exchange to which a reply should be sent.
    /// </summary>
    public string ReplyExchange { get; set; } = string.Empty;

    /// <summary>
    /// Routing key which that should be used for the reply.
    /// </summary>
    public string ReplyRoutingKey { get; set; } = string.Empty;

    /// <summary>
    /// Flag indicating whether to include the request payload in the response.
    /// </summary>
    public bool IncludeRequestPayloadInResponse { get; set; }

    /// <summary>
    /// Gets or sets the message properties. The default is <see cref="MessageProperties.Default" />
    /// </summary>
    public MessageProperties MessageProperties { get; set; }

    /// <summary>
    /// Optional headers in the form of a <see cref="Dictionary{TKey,TValue}" />
    /// whose generic type argument is <see cref="string" />.
    /// </summary>
    public IDictionary<string, string>? CustomHeader { get; set; }
}