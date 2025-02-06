using CarrotMQ.Core.Configuration;
using CarrotMQ.RabbitMQ.Configuration.Queues;

namespace CarrotMQ.RabbitMQ.Configuration.Exchanges;

/// <summary>
/// Represents an existing topic exchange
/// </summary>
public class TopicExchange : Exchange
{
    internal TopicExchange(string name, BindingCollection bindingCollection) : base(
        name,
        bindingCollection)
    {
    }

    /// <summary>
    /// Bind this exchange to the given <paramref name="queue" /> with the given <paramref name="routingKey" />
    /// </summary>
    public TopicExchange BindToQueue(Queue queue, string routingKey)
    {
        BindingCollection.AddBinding(new BindingConfiguration(Name, queue.QueueName, routingKey));

        return this;
    }
}