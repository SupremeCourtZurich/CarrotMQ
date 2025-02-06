using CarrotMQ.Core.MessageProcessing;

namespace CarrotMQ.Core.Configuration;

/// <summary>
/// Message binding with a specific routing key.
/// </summary>
public class BindingConfiguration
{
    ///
    public BindingConfiguration(string exchange, string queue, string routingKey)
    {
        Exchange = exchange;
        Queue = queue;
        RoutingKey = routingKey;
    }

    /// <summary>
    /// Routing key
    /// </summary>
    /// <remarks>This may be null if <see cref="ResolveRoutingKey" /> has not been called beforehand</remarks>
    public string RoutingKey { get; protected set; }

    /// <summary>
    /// Name of the message queue associated with this binding.
    /// </summary>
    public string Queue { get; }

    /// <summary>
    /// Name of the message exchange associated with this binding.
    /// </summary>
    public string Exchange { get; }

    /// <summary>
    /// Can be overriden to set the <see cref="RoutingKey" /> using the provided <paramref name="routingKeyResolver" />.
    /// </summary>
    /// <param name="routingKeyResolver">Responsible for resolving the routing key.</param>
    public virtual void ResolveRoutingKey(IRoutingKeyResolver routingKeyResolver)
    {
        // Do nothing
    }
}

/// <summary>
/// Message binding based on the message type <typeparamref name="TMessage" />
/// </summary>
/// <typeparam name="TMessage">The type of message associated with the binding.</typeparam>
public sealed class BindingConfiguration<TMessage> : BindingConfiguration
{
    ///
    public BindingConfiguration(string exchange, string queueName) : base(exchange, queueName, string.Empty)
    {
    }

    /// <inheritdoc />
    public override void ResolveRoutingKey(IRoutingKeyResolver routingKeyResolver)
    {
        RoutingKey = routingKeyResolver.GetRoutingKey<TMessage>(Exchange);
    }
}