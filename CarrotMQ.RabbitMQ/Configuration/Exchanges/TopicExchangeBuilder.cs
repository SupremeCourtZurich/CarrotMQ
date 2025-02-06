using CarrotMQ.Core.Configuration;
using RabbitMQ.Client;

namespace CarrotMQ.RabbitMQ.Configuration.Exchanges;

/// <summary>
/// Builder for configuring a new topic exchange
/// </summary>
public class TopicExchangeBuilder : TopicExchange, IExchangeBuilder<TopicExchangeBuilder>
{
    internal ExchangeConfiguration Configuration;

    internal TopicExchangeBuilder(string name, BindingCollection bindingCollection) : base(
        name,
        bindingCollection)
    {
        Configuration = new ExchangeConfiguration(name, ExchangeType.Topic);
    }

    /// <inheritdoc />
    public TopicExchangeBuilder WithAutoDelete(bool autoDelete = true)
    {
        Configuration.AutoDelete = autoDelete;

        return this;
    }

    /// <inheritdoc />
    public TopicExchangeBuilder WithDurability(bool durable = true)
    {
        Configuration.Durable = durable;

        return this;
    }

    /// <inheritdoc />
    public TopicExchangeBuilder WithCustomArgument(string key, object value)
    {
        Configuration.Arguments[key] = value;

        return this;
    }
}