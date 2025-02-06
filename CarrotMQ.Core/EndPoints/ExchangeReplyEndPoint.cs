using System;

namespace CarrotMQ.Core.EndPoints;

/// <summary>
/// Represents a class for messaging exchange endpoints used for the reply messages (reply of
/// <see cref="Dto.ICommand{TCommand,TResponse,TEndPointDefinition}" /> or
/// <see cref="Dto.IQuery{TQuery,TResponse,TEndPointDefinition}" />)
/// </summary>
public sealed class ExchangeReplyEndPoint : ReplyEndPointBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExchangeReplyEndPoint" /> class.
    /// </summary>
    /// <param name="exchangeName">The name of the messaging exchange to which the reply will be sent</param>
    /// <param name="routingKey">The routing key to use for the reply message</param>
    /// <param name="includeRequestPayloadInResponse">A flag indicating whether to include the request payload in the response.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="exchangeName" /> is empty or whitespace.</exception>
    public ExchangeReplyEndPoint(string exchangeName, string? routingKey = null, bool includeRequestPayloadInResponse = false) : base(
        string.IsNullOrWhiteSpace(exchangeName)
            ? throw new ArgumentException($"{nameof(exchangeName)} must not be empty", nameof(exchangeName))
            : exchangeName,
        routingKey ?? string.Empty,
        includeRequestPayloadInResponse)
    {
    }
}