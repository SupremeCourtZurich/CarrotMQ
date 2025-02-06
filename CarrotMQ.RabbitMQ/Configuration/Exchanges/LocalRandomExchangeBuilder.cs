using CarrotMQ.Core.Configuration;

namespace CarrotMQ.RabbitMQ.Configuration.Exchanges;

/// <summary>
/// Builder for configuring a new "local random" exchange
/// </summary>
public class LocalRandomExchangeBuilder : LocalRandomExchange, IExchangeBuilder<LocalRandomExchangeBuilder>
{
    internal ExchangeConfiguration Configuration;

    internal LocalRandomExchangeBuilder(string name, BindingCollection bindingCollection) : base(
        name,
        bindingCollection)
    {
        Configuration = new ExchangeConfiguration(name, "x-local-random");
    }

    /// <inheritdoc />
    public LocalRandomExchangeBuilder WithAutoDelete(bool autoDelete = true)
    {
        Configuration.AutoDelete = autoDelete;

        return this;
    }

    /// <inheritdoc />
    public LocalRandomExchangeBuilder WithDurability(bool durable = true)
    {
        Configuration.Durable = durable;

        return this;
    }

    /// <inheritdoc />
    public LocalRandomExchangeBuilder WithCustomArgument(string key, object value)
    {
        Configuration.Arguments[key] = value;

        return this;
    }
}