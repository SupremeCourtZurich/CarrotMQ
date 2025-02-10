using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
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
using CarrotMQ.RabbitMQ.MessageProcessing;
using CarrotMQ.RabbitMQ.MessageProcessing.Delivery;
using CarrotMQ.RabbitMQ.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client.Events;

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
    private readonly IProtocolSerializer _protocolSerializer;
    private readonly QueueConfiguration _queueConfig;
    private readonly string _queueName;

    private IAckDelivery? _ackDelivery;
    private IConsumerChannel? _consumerChannel;
    private bool _disposed;
    private IRunningTaskRegistry? _runningTaskRegistry;

    public CarrotConsumer(
        QueueConfiguration queueConfig,
        IList<BindingConfiguration> bindingConfigs,
        IMessageDistributor messageDistributor,
        IBrokerConnection brokerConnection,
        IProtocolSerializer protocolSerializer,
        ILogger<CarrotConsumer> logger,
        ICarrotMetricsRecorder metricsRecorder,
        IOptions<CarrotTracingOptions> carrotTracingOptions)
    {
        _queueConfig = queueConfig;
        _bindingConfigs = bindingConfigs;
        _messageDistributor = messageDistributor;
        _brokerConnection = brokerConnection;
        _protocolSerializer = protocolSerializer;
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

        _ackDelivery = CreateAckDelivery();
        _runningTaskRegistry = new RunningTaskRegistry();
        await _consumerChannel.StartConsumingAsync(_queueName, _ackCount == 0, _prefetchCount, ConsumeAsync, _arguments).ConfigureAwait(false);
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

    private IAckDelivery CreateAckDelivery()
    {
        return _ackCount switch
        {
            0 => new AutoAckDelivery(_logger),
            1 => new SingleAckDelivery(_consumerChannel!),
            _ => new MultiAckDelivery(_consumerChannel!, _ackCount) // > 1
        };
    }

    private async Task StopAsync()
    {
        Debug.Assert(_consumerChannel != null, $"{nameof(_consumerChannel)} must not be null.");

        await _consumerChannel!.StopConsumingAsync().ConfigureAwait(false);
        await _runningTaskRegistry!.CompleteAddingAsync().ConfigureAwait(false);
        _ackDelivery!.Dispose();
        await _consumerChannel.DisposeAsync().ConfigureAwait(false);
        _consumerChannel = null;
    }

    private async Task ConsumeAsync(BasicDeliverEventArgs ea)
    {
        if (!_runningTaskRegistry!.TryAdd(ea))
        {
            // Do nothing. CarrotService is going to stop the consumer
            // if auto-ack is configured --> message is lost
            return;
        }

        var ackDelivery = _ackDelivery!;
        var deliveryStatus = DeliveryStatus.Reject;
        var messageId = Guid.Empty;
        long? startTime = null;
        try
        {
            startTime = _metricsRecorder.StartConsuming();

            var carrotMessage = DeserializeMessage(ea);

            if (!Guid.TryParse(ea.BasicProperties.MessageId, out messageId))
            {
                messageId = carrotMessage.Header.MessageId;
            }

            _metricsRecorder.RecordMessageType(carrotMessage.Header.CalledMethod);

            using var scope = _logger.BeginScope(carrotMessage);

            using var activity = CarrotActivityFactory.CreateConsumerActivity(
                carrotMessage.Header,
                _brokerConnection.ServiceName,
                _brokerConnection.VHost,
                _carrotTracingOptions);

            using var cts = new CancellationTokenSource(_messageProcessingTimeout);

            deliveryStatus = await _messageDistributor.DistributeAsync(carrotMessage, cts.Token)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception while consuming message {MessageId}; Exchange:{Exchange}, RoutingKey:{RoutingKey}, DeliveryTag:{DeliveryTag}, ConsumerTag:{ConsumerTag}",
                messageId,
                ea.Exchange,
                ea.RoutingKey,
                ea.DeliveryTag,
                ea.ConsumerTag);
        }
        finally
        {
            try
            {
                await ackDelivery.DeliverAsync(ea.DeliveryTag, deliveryStatus).ConfigureAwait(false);
                if (startTime is not null) _metricsRecorder.EndConsuming(startTime.Value, deliveryStatus);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Unhandled exception while delivering acknowledgement for message {MessageId}; Exchange:{Exchange}, RoutingKey:{RoutingKey}, DeliveryTag:{DeliveryTag}, ConsumerTag:{ConsumerTag}",
                    messageId,
                    ea.Exchange,
                    ea.RoutingKey,
                    ea.DeliveryTag,
                    ea.ConsumerTag);
            }
            finally
            {
                _runningTaskRegistry.Remove(ea);
            }
        }
    }

    private CarrotMessage DeserializeMessage(BasicDeliverEventArgs ea)
    {
#if NET
        string payload = Encoding.UTF8.GetString(ea.Body.Span);
#else
        var payload = Encoding.UTF8.GetString(ea.Body.ToArray());
#endif
        _logger.LogDebug("Consuming {Payload} ...", payload);

        var carrotMessage = _protocolSerializer.Deserialize(payload);
        if (!string.IsNullOrWhiteSpace(ea.BasicProperties.ReplyTo))
        {
            // apply reply exchange and routingKey for DirectReply scenario (ReplyTo is generated by RabbitMQ)
            carrotMessage.Header.ReplyExchange = string.Empty;
            carrotMessage.Header.ReplyRoutingKey = ea.BasicProperties.ReplyTo ?? string.Empty;
        }

        return carrotMessage;
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