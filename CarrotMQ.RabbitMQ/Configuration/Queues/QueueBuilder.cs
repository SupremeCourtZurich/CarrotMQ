using CarrotMQ.Core.EndPoints;
using CarrotMQ.RabbitMQ.Configuration.Exchanges;

namespace CarrotMQ.RabbitMQ.Configuration.Queues;

/// 
public abstract class QueueBuilder<T> : Queue<T>
    where T : QueueBuilder<T>
{
    /// 
    protected QueueBuilder(QueueConfiguration queueConfiguration) : base(queueConfiguration)
    {
    }

    /// <summary>
    /// Sets the dead letter exchange for the queue.
    /// </summary>
    /// <param name="deadLetterExchange">The name of the dead letter exchange.</param>
    public T WithDeadLetterExchange(string deadLetterExchange)
    {
        if (!string.IsNullOrWhiteSpace(deadLetterExchange))
        {
            QueueConfiguration.Arguments[QueueConfiguration.QueueArgumentNames.DeadLetterExchange] = deadLetterExchange;
        }

        return (T)this;
    }

    /// <summary>
    /// Sets the dead letter exchange for the queue.
    /// </summary>
    /// <param name="deadLetterExchangeEndPoint">The deadLetter exchange to use.</param>
    public T WithDeadLetterExchange(ExchangeEndPoint deadLetterExchangeEndPoint)
    {
        return WithDeadLetterExchange(deadLetterExchangeEndPoint.Exchange);
    }

    /// <summary>
    /// Sets the dead letter exchange for the queue.
    /// </summary>
    /// <typeparam name="TExchangeEndPoint">The type of the deadLetter exchange to use.</typeparam>
    public T WithDeadLetterExchange<TExchangeEndPoint>()
        where TExchangeEndPoint : ExchangeEndPoint, new()
    {
        return WithDeadLetterExchange(new TExchangeEndPoint());
    }

    /// <summary>
    /// Sets the dead letter exchange for the queue.
    /// </summary>
    public T WithDeadLetterExchange(Exchange deadLetterExchange)
    {
        return WithDeadLetterExchange(deadLetterExchange.Name);
    }

    /// <summary>
    /// Sets a custom argument for the queue.
    /// </summary>
    /// <param name="key">The key of the custom argument.</param>
    /// <param name="value">The value of the custom argument.</param>
    public T WithCustomArgument(string key, object value)
    {
        QueueConfiguration.Arguments[key] = value;

        return (T)this;
    }

    /// <summary>
    /// Activates the x-single-active-consumer option on the queue.
    /// </summary>
    /// <param name="enable">Determines whether to enable the option.</param>
    public T WithSingleActiveConsumer(bool enable = true)
    {
        return WithCustomArgument(QueueConfiguration.QueueArgumentNames.SingleActiveConsumer, enable);
    }

    /// <inheritdoc cref="QueueConfiguration.Durable" />
    public T WithDurability(bool durable = true)
    {
        QueueConfiguration.Durable = durable;

        return (T)this;
    }

    /// <inheritdoc cref="QueueConfiguration.Exclusive" />
    public T WithExclusivity(bool exclusive = true)
    {
        QueueConfiguration.Exclusive = exclusive;

        return (T)this;
    }

    /// <inheritdoc cref="QueueConfiguration.AutoDelete" />
    public T WithAutoDelete(bool autoDelete = true)
    {
        QueueConfiguration.AutoDelete = autoDelete;

        return (T)this;
    }
}