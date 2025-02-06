using System;
using CarrotMQ.Core;
using CarrotMQ.Core.Configuration;
using CarrotMQ.RabbitMQ.Configuration.Exchanges;
using CarrotMQ.RabbitMQ.Configuration.Queues;
using CarrotMQ.RabbitMQ.Connectivity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CarrotMQ.RabbitMQ.Configuration;

/// <summary>
/// Builder class for configuring and setting up <b>CarrotMQ</b>
/// </summary>
public sealed class CarrotConfigurationBuilder
{
    private readonly BindingCollection _bindings;
    private readonly IServiceCollection _serviceCollection;
    private Action<BrokerConnectionOptions>? _brokerConnectionConfigAction;
    private string _brokerConnectionSectionName = BrokerConnectionOptions.BrokerConnection;

    /// <summary>
    /// Initializes a new instance of the <see cref="CarrotConfigurationBuilder" /> class with the specified
    /// <see cref="IServiceCollection" />.
    /// </summary>
    /// <param name="serviceCollection">The service collection to configure.</param>
    public CarrotConfigurationBuilder(IServiceCollection serviceCollection)
    {
        _serviceCollection = serviceCollection;

        _bindings = new BindingCollection();
        Exchanges = new ExchangeCollection(_bindings);
        Queues = new QueueCollection();
        Handlers = new HandlerCollection(serviceCollection, _bindings);
    }

    /// <summary>
    /// Exchange configurations
    /// </summary>
    public ExchangeCollection Exchanges { get; }

    /// <summary>
    /// Queues and consumer configurations
    /// </summary>
    public QueueCollection Queues { get; }

    /// <summary>
    /// Handler configurations
    /// </summary>
    public HandlerCollection Handlers { get; }

    /// <summary>
    /// Configure the connection to the broker
    /// </summary>
    /// <param name="sectionName">
    /// Name of the section from where the configuration <see cref="BrokerConnectionOptions" />
    /// should be loaded.
    /// </param>
    /// <param name="configureOptions">Overriding the <see cref="BrokerConnectionOptions" /> loaded from configuration.</param>
    /// <remarks>
    /// Optional: if not called the configuration will be loaded from the default section
    /// <see cref="BrokerConnectionOptions.BrokerConnection" />
    /// </remarks>
    public void ConfigureBrokerConnection(
        string sectionName = BrokerConnectionOptions.BrokerConnection,
        Action<BrokerConnectionOptions>? configureOptions = null)
    {
        _brokerConnectionSectionName = sectionName;
        _brokerConnectionConfigAction = configureOptions;
    }

    /// <summary>
    /// Configures CarrotMQ instrumentation options
    /// </summary>
    /// <param name="sectionName">
    /// The configuration section name for CarrotMQ tracing options. Defaults to
    /// <see cref="CarrotTracingOptions.CarrotTracing" />.
    /// </param>
    /// <param name="configureOptions">
    /// An optional action to configure additional CarrotMQ tracing options.
    /// This action is invoked after the default options are loaded from configuration.
    /// </param>
    public CarrotConfigurationBuilder ConfigureTracing(
        string sectionName = CarrotTracingOptions.CarrotTracing,
        Action<CarrotTracingOptions>? configureOptions = null)
    {
        _serviceCollection.AddOptions<CarrotTracingOptions>()
            .BindConfiguration(sectionName)
            .Configure(options => configureOptions?.Invoke(options))
            .ValidateOnStart();

        return this;
    }

    /// <summary>
    /// Building the configuration and adding them to the <see cref="IServiceCollection" />
    /// </summary>
    internal void Build()
    {
        AddBrokerConnection();

        Queues.Validate();

        _serviceCollection.AddSingleton(Queues);
        _serviceCollection.AddSingleton(_bindings);
        _serviceCollection.AddSingleton(Handlers);
        _serviceCollection.AddSingleton(Exchanges);
    }

    private void AddBrokerConnection()
    {
        _serviceCollection.AddOptions<BrokerConnectionOptions>()
            .BindConfiguration(_brokerConnectionSectionName)
            .Configure(options => _brokerConnectionConfigAction?.Invoke(options))
            .ValidateOnStart();

        _serviceCollection.TryAddSingleton<IBrokerConnection, BrokerConnection>();
        _serviceCollection.TryAddSingleton<IEndpointResolver, DefaultCarrotEndPointResolver>();
        _serviceCollection.AddSingleton<IValidateOptions<BrokerConnectionOptions>, BrokerConnectionOptionsValidation>();
    }

    /// <summary>
    /// Initializes <see cref="CarrotService" /> as <see cref="IHostedService" /> which runs the configured RabbitMQ consumer
    /// </summary>
    public CarrotConfigurationBuilder StartAsHostedService()
    {
        _serviceCollection.AddHostedService<CarrotService>();

        return this;
    }
}