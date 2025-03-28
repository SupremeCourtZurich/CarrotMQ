using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Common;
using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Telemetry;
using CarrotMQ.RabbitMQ.Configuration.Queues;
using CarrotMQ.RabbitMQ.Connectivity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarrotMQ.RabbitMQ;

internal sealed class CarrotConsumer : IAsyncDisposable
{
    private readonly ushort _ackCount;
    private readonly IDictionary<string, object?> _arguments;
    private readonly IList<BindingConfiguration> _bindingConfigs;
    private readonly IBrokerConnection _brokerConnection;
    private readonly IOptions<CarrotTracingOptions> _carrotTracingOptions;
    private readonly AsyncLock _consumerLock = new();
    private readonly ILogger<CarrotConsumer> _logger;
    private readonly IMessageDistributor _messageDistributor;
    private readonly TimeSpan _messageProcessingTimeout;
    private readonly ICarrotMetricsRecorder _metricsRecorder;
    private readonly ushort _prefetchCount;
    private readonly QueueConfiguration _queueConfig;
    private readonly string _queueName;

    private IConsumerChannel? _consumerChannel;
    private bool _disposed;

    public CarrotConsumer(
        QueueConfiguration queueConfig,
        IList<BindingConfiguration> bindingConfigs,
        IMessageDistributor messageDistributor,
        IBrokerConnection brokerConnection,
        ILogger<CarrotConsumer> logger,
        ICarrotMetricsRecorder metricsRecorder,
        IOptions<CarrotTracingOptions> carrotTracingOptions)
    {
        _queueConfig = queueConfig;
        _bindingConfigs = bindingConfigs;
        _messageDistributor = messageDistributor;
        _brokerConnection = brokerConnection;
        _logger = logger;
        _metricsRecorder = metricsRecorder;
        _carrotTracingOptions = carrotTracingOptions;

        Debug.Assert(_queueConfig.ConsumerConfiguration != null, $"{nameof(_queueConfig.ConsumerConfiguration)} must not be null.");
        _queueName = _queueConfig.QueueName;
        _prefetchCount = _queueConfig.ConsumerConfiguration!.PrefetchCount;
        _ackCount = _queueConfig.ConsumerConfiguration.AckCount;
        _arguments = _queueConfig.ConsumerConfiguration.Arguments;
        _messageProcessingTimeout = _queueConfig.ConsumerConfiguration.MessageProcessingTimeout;
    }

    public async Task InitializeAsync()
    {
        Debug.Assert(!_disposed, $"{nameof(CarrotConsumer)} must not be disposed.");
        _logger.LogInformation(
            "Initialize consumer. Queue:{Name}, PrefetchCount:{PrefetchCount}, AckCount:{AckCount}",
            _queueName,
            _prefetchCount,
            _ackCount);

        using var scope = await _consumerLock.LockAsync().ConfigureAwait(false);

        await StartAsync().ConfigureAwait(false);
    }

    private async Task StartAsync()
    {
        Debug.Assert(_consumerChannel == null, $"{nameof(_consumerChannel)} must be null.");

        if (_disposed) return;

        _consumerChannel = await _brokerConnection.CreateConsumerChannelAsync().ConfigureAwait(false);
        _consumerChannel.UnregisteredAsync += ConsumerChannelUnregisteredAsync;
        await _consumerChannel.ApplyConfigurations(_queueConfig, _bindingConfigs).ConfigureAwait(false);

        await _consumerChannel.StartConsumingAsync(_queueName, _ackCount, _prefetchCount, ConsumeAsync, _arguments).ConfigureAwait(false);
    }

    private async Task ConsumerChannelUnregisteredAsync(object sender, EventArgs eventArgs)
    {
        using var scope = await _consumerLock.LockAsync().ConfigureAwait(false);
        if (_consumerChannel is { IsOpen: true })
        {
            await StopAsync().ConfigureAwait(false);
            await Task.Delay(_brokerConnection.NetworkRecoveryInterval, CancellationToken.None).ConfigureAwait(false);
            await StartAsync().ConfigureAwait(false);
        }
    }

    private async Task StopAsync()
    {
        Debug.Assert(_consumerChannel != null, $"{nameof(_consumerChannel)} must not be null.");

        await _consumerChannel!.StopConsumingAsync().ConfigureAwait(false);
        await _consumerChannel.DisposeAsync().ConfigureAwait(false);
        _consumerChannel = null;
    }

    private async Task<DeliveryStatus> ConsumeAsync(CarrotMessage message)
    {
        var deliveryStatus = DeliveryStatus.Reject;
        var messageId = Guid.Empty;
        long? startTime = null;
        try
        {
            startTime = _metricsRecorder.StartConsuming();

            _metricsRecorder.RecordMessageType(message.Header.CalledMethod);

            using var scope = _logger.BeginScope(message);

            using var activity = CarrotActivityFactory.CreateConsumerActivity(
                message.Header,
                _brokerConnection.ServiceName,
                _brokerConnection.VHost,
                _carrotTracingOptions);

            using var cts = new CancellationTokenSource(_messageProcessingTimeout);

            deliveryStatus = await _messageDistributor.DistributeAsync(message, cts.Token)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception while consuming message {MessageId}; Exchange:{Exchange}, RoutingKey:{RoutingKey}",
                messageId,
                message.Header.Exchange,
                message.Header.RoutingKey);
        }
        finally
        {
            try
            {
                if (startTime is not null) _metricsRecorder.EndConsuming(startTime.Value, deliveryStatus);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Unhandled exception while delivering acknowledgement for message {MessageId}; Exchange:{Exchange}, RoutingKey:{RoutingKey}",
                    messageId,
                    message.Header.Exchange,
                    message.Header.RoutingKey);
            }

        }

        return deliveryStatus;
    }

    public async ValueTask DisposeAsync()
    {
        using var scope = await _consumerLock.LockAsync().ConfigureAwait(false);

        if (!_disposed)
        {
            await StopAsync().ConfigureAwait(false);

            _disposed = true;
        }
    }
}