using System.Collections.Generic;
using System.Linq;
using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.EndPoints;

namespace CarrotMQ.RabbitMQ.Configuration.Exchanges;

///
public sealed class ExchangeCollection
{
    private readonly BindingCollection _bindingCollection;
    private readonly Dictionary<string, ExchangeConfiguration> _exchangeConfigurations = [];

    internal ExchangeCollection(BindingCollection bindingCollection)
    {
        _bindingCollection = bindingCollection;
    }

    /// <summary>
    /// Gets a list of exchange configurations in the configuration.
    /// </summary>
    /// <returns>A list of exchange configurations.</returns>
    internal List<ExchangeConfiguration> GetExchangeConfigurations()
    {
        return _exchangeConfigurations.Values.ToList();
    }

    /// <summary>
    /// Adds a new direct exchange to the service configuration.
    /// </summary>
    /// <typeparam name="TExchangeEndPoint">The type of the exchange endpoint used to determine the exchange name.</typeparam>
    /// <returns>The configured <see cref="DirectExchangeBuilder" />.</returns>
    public DirectExchangeBuilder AddDirect<TExchangeEndPoint>()
        where TExchangeEndPoint : ExchangeEndPoint, new()
    {
        TExchangeEndPoint endPoint = new();

        return AddDirect(endPoint.Exchange);
    }

    /// <summary>
    /// Adds a new direct exchange to the service configuration.
    /// </summary>
    /// <param name="exchange">The name of the exchange.</param>
    /// <returns>The configured <see cref="DirectExchangeBuilder" />.</returns>
    public DirectExchangeBuilder AddDirect(string exchange)
    {
        var exchangeConfigurationBuilder = new DirectExchangeBuilder(
            exchange,
            _bindingCollection);

        AddExchangeConfiguration(exchangeConfigurationBuilder.Configuration);

        return exchangeConfigurationBuilder;
    }

    /// <summary>
    /// Adds a new fanout exchange to the service configuration.
    /// </summary>
    /// <typeparam name="TExchangeEndPoint">The type of the exchange endpoint used to determine the exchange name.</typeparam>
    /// <returns>The configured <see cref="FanOutExchangeBuilder" />.</returns>
    public FanOutExchangeBuilder AddFanOut<TExchangeEndPoint>()
        where TExchangeEndPoint : ExchangeEndPoint, new()
    {
        TExchangeEndPoint endPoint = new();

        return AddFanOut(endPoint.Exchange);
    }

    /// <summary>
    /// Adds a new fanout exchange to the service configuration.
    /// </summary>
    /// <param name="exchange">The name of the exchange.</param>
    /// <returns>The configured <see cref="FanOutExchangeBuilder" />.</returns>
    public FanOutExchangeBuilder AddFanOut(string exchange)
    {
        var exchangeConfigurationBuilder = new FanOutExchangeBuilder(
            exchange,
            _bindingCollection);

        AddExchangeConfiguration(exchangeConfigurationBuilder.Configuration);

        return exchangeConfigurationBuilder;
    }

    /// <summary>
    /// Adds a new topic exchange to the service configuration.
    /// </summary>
    /// <typeparam name="TExchangeEndPoint">The type of the exchange endpoint used to determine the exchange name.</typeparam>
    /// <returns>The configured <see cref="TopicExchangeBuilder" />.</returns>
    public TopicExchangeBuilder AddTopic<TExchangeEndPoint>()
        where TExchangeEndPoint : ExchangeEndPoint, new()
    {
        TExchangeEndPoint endPoint = new();

        return AddTopic(endPoint.Exchange);
    }

    /// <summary>
    /// Adds a new topic exchange to the service configuration.
    /// </summary>
    /// <param name="exchange">The name of the exchange.</param>
    /// <returns>The configured <see cref="TopicExchangeBuilder" />.</returns>
    public TopicExchangeBuilder AddTopic(string exchange)
    {
        var exchangeConfigurationBuilder = new TopicExchangeBuilder(
            exchange,
            _bindingCollection);

        AddExchangeConfiguration(exchangeConfigurationBuilder.Configuration);

        return exchangeConfigurationBuilder;
    }

    /// <summary>
    /// Adds a new "local random" exchange to the service configuration.
    /// </summary>
    /// <typeparam name="TExchangeEndPoint">The type of the exchange endpoint used to determine the exchange name.</typeparam>
    /// <returns>The configured <see cref="LocalRandomExchangeBuilder" />.</returns>
    public LocalRandomExchangeBuilder AddLocalRandom<TExchangeEndPoint>()
        where TExchangeEndPoint : ExchangeEndPoint, new()
    {
        TExchangeEndPoint endPoint = new();

        return AddLocalRandom(endPoint.Exchange);
    }

    /// <summary>
    /// Adds a new "local random" exchange to the service configuration.
    /// </summary>
    /// <param name="exchange">The name of the exchange.</param>
    /// <returns>The configured <see cref="LocalRandomExchangeBuilder" />.</returns>
    public LocalRandomExchangeBuilder AddLocalRandom(string exchange)
    {
        var exchangeConfigurationBuilder = new LocalRandomExchangeBuilder(
            exchange,
            _bindingCollection);

        AddExchangeConfiguration(exchangeConfigurationBuilder.Configuration);

        return exchangeConfigurationBuilder;
    }

    internal void AddExchangeConfiguration(ExchangeConfiguration exchangeOptions)
    {
        if (!_exchangeConfigurations.TryAdd(exchangeOptions.Name, exchangeOptions))
        {
            throw new DuplicateExchangeException(exchangeOptions.Name);
        }
    }

    /// <summary>
    /// Use an existing direct exchange.
    /// </summary>
    /// <typeparam name="TExchangeEndPoint">The type of the exchange endpoint used to determine the exchange name.</typeparam>
    /// <returns>The <see cref="DirectExchange" /> for the existing exchange.</returns>
    /// <remarks>
    /// The exchange will not be created if it does not exist.
    /// </remarks>
    public DirectExchange UseDirect<TExchangeEndPoint>() where TExchangeEndPoint : ExchangeEndPoint, new() =>
        UseDirect(new TExchangeEndPoint().Exchange);

    /// <summary>
    /// Use an existing direct exchange.
    /// </summary>
    /// <param name="exchange">The name of the exchange.</param>
    /// <returns>The <see cref="DirectExchange" /> for the existing exchange.</returns>
    /// <remarks>The exchange will not be created if it does not exist.</remarks>
    public DirectExchange UseDirect(string exchange) => new(exchange, _bindingCollection);

    /// <summary>
    /// Use an existing fanout exchange.
    /// </summary>
    /// <typeparam name="TExchangeEndPoint">The type of the exchange endpoint used to determine the exchange name.</typeparam>
    /// <returns>The <see cref="FanOutExchange" /> for the existing exchange.</returns>
    /// <remarks>
    /// The exchange will not be created if it does not exist.
    /// </remarks>
    public FanOutExchange UseFanOut<TExchangeEndPoint>()
        where TExchangeEndPoint : ExchangeEndPoint, new() => UseFanOut(new TExchangeEndPoint().Exchange);

    /// <summary>
    /// Use an existing fanout exchange.
    /// </summary>
    /// <param name="exchange">The name of the exchange.</param>
    /// <returns>The <see cref="FanOutExchange" /> for the existing exchange.</returns>
    /// <remarks>The exchange will not be created if it does not exist.</remarks>
    public FanOutExchange UseFanOut(string exchange) => new(exchange, _bindingCollection);

    /// <summary>
    /// Use an existing topic exchange.
    /// </summary>
    /// <typeparam name="TExchangeEndPoint">The type of the exchange endpoint used to determine the exchange name.</typeparam>
    /// <returns>The <see cref="TopicExchange" /> for the existing exchange.</returns>
    /// <remarks>
    /// The exchange will not be created if it does not exist.
    /// </remarks>
    public TopicExchange UseTopic<TExchangeEndPoint>()
        where TExchangeEndPoint : ExchangeEndPoint, new() => UseTopic(new TExchangeEndPoint().Exchange);

    /// <summary>
    /// Use an existing topic exchange.
    /// </summary>
    /// <param name="exchange">The name of the exchange.</param>
    /// <returns>The <see cref="TopicExchange" /> for the existing exchange.</returns>
    /// <remarks>The exchange will not be created if it does not exist.</remarks>
    public TopicExchange UseTopic(string exchange) => new(exchange, _bindingCollection);

    /// <summary>
    /// Use an existing "local random" exchange.
    /// </summary>
    /// <typeparam name="TExchangeEndPoint">The type of the exchange endpoint used to determine the exchange name.</typeparam>
    /// <returns>The <see cref="LocalRandomExchange" /> for the existing exchange.</returns>
    /// <remarks>
    /// The exchange will not be created if it does not exist.
    /// </remarks>
    public LocalRandomExchange UseLocalRandom<TExchangeEndPoint>()
        where TExchangeEndPoint : ExchangeEndPoint, new() => UseLocalRandom(new TExchangeEndPoint().Exchange);

    /// <summary>
    /// Use an existing "local random" exchange.
    /// </summary>
    /// <param name="exchange">The name of the exchange.</param>
    /// <returns>The <see cref="LocalRandomExchange" /> for the existing exchange.</returns>
    /// <remarks>The exchange will not be created if it does not exist.</remarks>
    public LocalRandomExchange UseLocalRandom(string exchange) => new(exchange, _bindingCollection);
}