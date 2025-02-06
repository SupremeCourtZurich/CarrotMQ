using CarrotMQ.Core.MessageProcessing;

namespace CarrotMQ.Core.EndPoints;

/// <summary>
/// Represents the base class for messaging endpoints.
/// </summary>
public abstract class EndPointBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EndPointBase" /> class with the specified exchange.
    /// </summary>
    /// <param name="exchangeName">Exchange associated with the endpoint.</param>
    protected EndPointBase(string exchangeName)
    {
        Exchange = exchangeName;
    }

    /// <summary>
    /// Exchange associated with the endpoint.
    /// </summary>
    public string Exchange { get; }

    /// <summary>
    /// Gets the routing key for a specified message using the provided <see cref="IRoutingKeyResolver" />.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message for which the routing key is generated.</typeparam>
    /// <param name="routingKeyResolver">The resolver used to determine the routing key.</param>
    /// <returns>The routing key for the specified message.</returns>
    public abstract string GetRoutingKey<TMessage>(IRoutingKeyResolver routingKeyResolver);
}