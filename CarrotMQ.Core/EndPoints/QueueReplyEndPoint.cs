using System;

namespace CarrotMQ.Core.EndPoints;

/// <summary>
/// Represents a class for messaging queue endpoints used for the reply messages (reply of
/// <see cref="Dto.ICommand{TCommand,TResponse,TEndPointDefinition}" /> or
/// <see cref="Dto.IQuery{TQuery,TResponse,TEndPointDefinition}" />)
/// </summary>
public sealed class QueueReplyEndPoint : ReplyEndPointBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueReplyEndPoint" /> class.
    /// </summary>
    /// <param name="queueName">The name of the messaging queue to which the reply will be sent</param>
    /// <param name="includeRequestPayloadInResponse">A flag indicating whether to include the request payload in the response.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="queueName" /> is empty or whitespace.</exception>
    public QueueReplyEndPoint(string queueName, bool includeRequestPayloadInResponse = false) : base(
        string.Empty,
        string.IsNullOrWhiteSpace(queueName) ? throw new ArgumentException($"{nameof(queueName)} must not be empty", nameof(queueName)) : queueName,
        includeRequestPayloadInResponse)
    {
    }
}