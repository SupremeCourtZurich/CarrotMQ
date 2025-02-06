using CarrotMQ.Core.Common;

namespace CarrotMQ.Core.MessageProcessing;

/// <summary>
/// Default implementation of <see cref="IRoutingKeyResolver" /> that generates routing keys
/// based on the FullName of the specified request type, ensuring the length is within the allowed limit.
/// </summary>
public class DefaultRoutingKeyResolver : IRoutingKeyResolver
{
    /// <summary>
    /// The maximum length allowed for a routing key as defined in the AMQP Protocol
    /// </summary>
    public const int MaxLength = 256;

    /// <summary>
    /// The ellipsis used to indicate truncated content in the routing key.
    /// </summary>
    public const string Ellipsis = "...";

    /// <summary>
    /// Gets the routing key based on the FullName of the specified request type.
    /// If the FullName exceeds the maximum length, the namespace part is truncated.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request for which to generate the routing key.</typeparam>
    /// <param name="exchangeName">The name of the messaging exchange associated with the routing key.</param>
    /// <returns>The generated routing key.</returns>
    public string GetRoutingKey<TRequest>(string exchangeName)
    {
        var requestType = typeof(TRequest);

        var routingKey = requestType.FullName ?? string.Empty;

        if (routingKey.Length is > 0 and <= MaxLength)
        {
            return routingKey;
        }

        var typeName = requestType.Name.Truncate(MaxLength)!;
        var maxNamespaceLength = MaxLength - typeName.Length - Ellipsis.Length;

        if (maxNamespaceLength <= 0)
        {
            return typeName;
        }

        var namespaceName = requestType.Namespace ?? string.Empty;
        if (requestType.DeclaringType != null)
        {
            namespaceName = requestType.DeclaringType.FullName ?? requestType.DeclaringType.Name;
        }

        routingKey = $"{namespaceName.Truncate(maxNamespaceLength)}{Ellipsis}{typeName}";

        return routingKey;
    }
}