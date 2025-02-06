using System;
using CarrotMQ.Core.MessageProcessing;

namespace CarrotMQ.Core.EndPoints;

/// <summary>
/// Represents an abstract class for exchange endpoints.
/// </summary>
public abstract class ExchangeEndPoint : EndPointBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExchangeEndPoint" /> class with the specified exchange name.
    /// </summary>
    /// <param name="exchangeName">The name of the messaging exchange acting as endpoint.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="exchangeName" /> is empty or whitespace.</exception>
    protected ExchangeEndPoint(string exchangeName) : base(
        string.IsNullOrWhiteSpace(exchangeName)
            ? throw new ArgumentException($"{nameof(exchangeName)} must not be empty", nameof(exchangeName))
            : exchangeName)
    {
    }

    /// <inheritdoc cref="EndPointBase.GetRoutingKey{TMessage}" />
    public override string GetRoutingKey<TMessage>(IRoutingKeyResolver routingKeyResolver)
    {
        return routingKeyResolver.GetRoutingKey<TMessage>(Exchange);
    }
}