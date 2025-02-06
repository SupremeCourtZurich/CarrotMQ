using CarrotMQ.Core.Configuration;
using RabbitMQ.Client;

namespace CarrotMQ.RabbitMQ.Configuration.Exchanges;

/// <summary>
/// Builder for configuring a fanOut exchange
/// </summary>
public class FanOutExchangeBuilder : FanOutExchange, IExchangeBuilder<FanOutExchangeBuilder>
{
    internal ExchangeConfiguration Configuration;

    internal FanOutExchangeBuilder(string name, BindingCollection bindingCollection) : base(
        name,
        bindingCollection)
    {
        Configuration = new ExchangeConfiguration(name, ExchangeType.Fanout);
    }

    /// <inheritdoc />
    public FanOutExchangeBuilder WithAutoDelete(bool autoDelete = true)
    {
        Configuration.AutoDelete = autoDelete;

        return this;
    }

    /// <inheritdoc />
    public FanOutExchangeBuilder WithDurability(bool durable = true)
    {
        Configuration.Durable = durable;

        return this;
    }

    /// <inheritdoc />
    public FanOutExchangeBuilder WithCustomArgument(string key, object value)
    {
        Configuration.Arguments[key] = value;

        return this;
    }
}