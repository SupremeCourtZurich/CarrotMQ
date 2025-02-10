using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CarrotMQ.Core;
using CarrotMQ.Core.Common;
using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.Telemetry;
using CarrotMQ.RabbitMQ.Configuration.Exchanges;
using CarrotMQ.RabbitMQ.Configuration.Queues;
using CarrotMQ.RabbitMQ.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarrotMQ.RabbitMQ.Connectivity;

internal class CarrotConsumerManager : ICarrotConsumerManager
{
    private readonly BindingCollection _bindingCollection;
    private readonly IBrokerConnection _brokerConnection;
    private readonly ICarrotMetricsRecorder _carrotMetricsRecorder;
    private readonly IOptions<CarrotTracingOptions> _carrotTracingOptions;
    private readonly AsyncLock _consumerLock = new();
    private readonly List<CarrotConsumer> _consumers = new();
    private readonly ExchangeCollection _exchangeCollection;
    private readonly ILogger<CarrotConsumerManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IMessageDistributor _messageDistributor;
    private readonly IProtocolSerializer _protocolSerializer;

    private readonly QueueCollection _queueCollection;

    public CarrotConsumerManager(
        BindingCollection bindingCollection,
        ExchangeCollection exchangeCollection,
        QueueCollection queueCollection,
        IMessageDistributor messageDistributor,
        IBrokerConnection brokerConnection,
        IProtocolSerializer protocolSerializer,
        ICarrotMetricsRecorder carrotMetricsRecorder,
        IRoutingKeyResolver routingKeyResolver,
        IOptions<CarrotTracingOptions> carrotTracingOptions,
        ILoggerFactory loggerFactory)
    {
        _bindingCollection = bindingCollection;
        _exchangeCollection = exchangeCollection;
        _queueCollection = queueCollection;
        _messageDistributor = messageDistributor;
        _brokerConnection = brokerConnection;
        _protocolSerializer = protocolSerializer;
        _carrotMetricsRecorder = carrotMetricsRecorder;
        _carrotTracingOptions = carrotTracingOptions;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<CarrotConsumerManager>();

        _bindingCollection.ResolveRoutingKeys(routingKeyResolver);
        _brokerConnection.ConnectionClosing += ConnectionClosing;
    }

    private async Task ConnectionClosing(object sender, EventArgs e)
    {
        await StopConsumingAsync().ConfigureAwait(false);
    }

    public async Task StartConsumingAsync()
    {
        using var lockInstance = await _consumerLock.LockAsync().ConfigureAwait(false);

        if (_consumers.Count > 0) return;

        _logger.LogDebug("Starting consumers...");

        var publisherChannel = await _brokerConnection.GetPublisherChannelAsync().ConfigureAwait(false);
        await publisherChannel.DeclareExchangesAsync(_exchangeCollection).ConfigureAwait(false);

        foreach (var queueConsumerConfig in _queueCollection.GetQueueConfigurations())
        {
            var bindingsForCurrentQueue = _bindingCollection.GetBindingsForQueue(queueConsumerConfig.QueueName);

            if (queueConsumerConfig.ConsumerConfiguration != null)
            {
                var carrotConsumer = new CarrotConsumer(
                    queueConsumerConfig,
                    bindingsForCurrentQueue,
                    _messageDistributor,
                    _brokerConnection,
                    _protocolSerializer,
                    _loggerFactory.CreateLogger<CarrotConsumer>(),
                    _carrotMetricsRecorder,
                    _carrotTracingOptions);
                _consumers.Add(carrotConsumer);

                await carrotConsumer.InitializeAsync().ConfigureAwait(false);
            }
            else
            {
                await publisherChannel.ApplyConfigurations(queueConsumerConfig, bindingsForCurrentQueue).ConfigureAwait(false);
            }
        }

        _logger.LogDebug("Consumers started.");
    }

    public async Task StopConsumingAsync()
    {
        using var lockInstance = await _consumerLock.LockAsync().ConfigureAwait(false);

        if (_consumers.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Stopping consumers...");

        foreach (var carrotConsumer in _consumers)
        {
            await carrotConsumer.DisposeAsync().ConfigureAwait(false);
        }

        _consumers.Clear();
        _logger.LogDebug("Consumers stopped.");
    }
}