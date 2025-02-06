using CarrotMQ.Core.Configuration;
using RabbitMQ.Client;

namespace CarrotMQ.RabbitMQ.Configuration.Exchanges;

/// <summary>
/// Builder for configuring a new direct exchange
/// </summary>
public class DirectExchangeBuilder : DirectExchange, IExchangeBuilder<DirectExchangeBuilder>
{
    internal ExchangeConfiguration Configuration;

    internal DirectExchangeBuilder(string name, BindingCollection bindingCollection) : base(
        name,
        bindingCollection)
    {
        Configuration = new ExchangeConfiguration(name, ExchangeType.Direct);
    }

    /// <inheritdoc />
    public DirectExchangeBuilder WithAutoDelete(bool autoDelete = true)
    {
        Configuration.AutoDelete = autoDelete;

        return this;
    }

    /// <inheritdoc />
    public DirectExchangeBuilder WithDurability(bool durable = true)
    {
        Configuration.Durable = durable;

        return this;
    }

    /// <inheritdoc />
    public DirectExchangeBuilder WithCustomArgument(string key, object value)
    {
        Configuration.Arguments[key] = value;

        return this;
    }
}