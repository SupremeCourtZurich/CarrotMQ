namespace CarrotMQ.RabbitMQ.Configuration.Exchanges;

/// 
public interface IExchangeBuilder<out TExchangeBuilder> where TExchangeBuilder : IExchangeBuilder<TExchangeBuilder>
{
    /// <inheritdoc cref="ExchangeConfiguration.AutoDelete" />
    TExchangeBuilder WithAutoDelete(bool autoDelete = true);

    /// <inheritdoc cref="ExchangeConfiguration.Durable" />
    TExchangeBuilder WithDurability(bool durable = true);

    /// <summary>
    /// Sets a custom argument for the exchange.
    /// </summary>
    /// <param name="key">The key of the custom argument.</param>
    /// <param name="value">The value of the custom argument.</param>
    TExchangeBuilder WithCustomArgument(string key, object value);
}