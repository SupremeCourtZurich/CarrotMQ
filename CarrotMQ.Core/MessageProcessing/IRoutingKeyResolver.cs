namespace CarrotMQ.Core.MessageProcessing;

/// <summary>
/// Interface that generates routing key for given message type and/or exchange name.
/// </summary>
public interface IRoutingKeyResolver
{
    /// <summary>
    /// Gets the routing key based on the request type and/or the exchange name.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request for which to generate the routing key.</typeparam>
    /// <param name="exchangeName">The name of the messaging exchange associated with the routing key.</param>
    /// <returns>The routing key.</returns>
    string GetRoutingKey<TRequest>(string exchangeName);
}