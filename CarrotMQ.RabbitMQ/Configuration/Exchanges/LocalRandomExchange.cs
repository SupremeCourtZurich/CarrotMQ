using CarrotMQ.Core.Configuration;
using CarrotMQ.RabbitMQ.Configuration.Queues;

namespace CarrotMQ.RabbitMQ.Configuration.Exchanges;

/// <summary>
/// Represents an existing "local random" exchange
/// </summary>
public class LocalRandomExchange : Exchange
{
    internal LocalRandomExchange(string name, BindingCollection bindingCollection) : base(
        name,
        bindingCollection)
    {
    }

    /// <summary>
    /// Bind this exchange to the given <paramref name="queue" />
    /// </summary>
    public LocalRandomExchange BindToQueue(Queue queue)
    {
        BindingCollection.AddBinding(new BindingConfiguration(Name, queue.QueueName, string.Empty));

        return this;
    }
}