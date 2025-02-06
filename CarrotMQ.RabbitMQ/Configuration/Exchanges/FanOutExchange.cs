using CarrotMQ.Core.Configuration;
using CarrotMQ.RabbitMQ.Configuration.Queues;

namespace CarrotMQ.RabbitMQ.Configuration.Exchanges;

/// <summary>
/// Represents an existing fanOut exchange
/// </summary>
public class FanOutExchange : Exchange
{
    internal FanOutExchange(string name, BindingCollection bindingCollection) : base(
        name,
        bindingCollection)
    {
    }

    /// <summary>
    /// Bind this exchange to the given <paramref name="queue" />
    /// </summary>
    public FanOutExchange BindToQueue(Queue queue)
    {
        BindingCollection.AddBinding(new BindingConfiguration(Name, queue.QueueName, string.Empty));

        return this;
    }
}