using System;
using CarrotMQ.Core.MessageProcessing;

namespace CarrotMQ.Core.EndPoints;

/// <summary>
/// Represents an abstract class for queue endpoints.
/// </summary>
public abstract class QueueEndPoint : EndPointBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QueueEndPoint" /> class with the specified queue name.
    /// </summary>
    /// <param name="queueName">The name of the messaging queue acting as endpoint</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="queueName" /> is empty or whitespace.</exception>
    protected QueueEndPoint(string queueName) : base(string.Empty)
    {
        QueueName = string.IsNullOrWhiteSpace(queueName)
            ? throw new ArgumentException($"{nameof(queueName)} must not be empty", nameof(queueName))
            : queueName;
    }

    /// <summary>
    /// Gets the name of the messaging queue
    /// </summary>
    public string QueueName { get; }

    /// <summary>
    /// Gets the routing key <see cref="QueueEndPoint" /> -> the <see cref="QueueName" />
    /// </summary>
    /// <typeparam name="TMessage">The type of the message for which the routing key is generated.</typeparam>
    /// <param name="routingKeyResolver">The resolver is not used in with this endpoint type</param>
    /// <returns>The <see cref="QueueName" /></returns>
    public override string GetRoutingKey<TMessage>(IRoutingKeyResolver routingKeyResolver)
    {
        return QueueName;
    }
}