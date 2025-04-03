using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CarrotMQ.Core.Common;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.MessageProcessing;
using CarrotMQ.RabbitMQ.MessageProcessing.Delivery;
using CarrotMQ.RabbitMQ.Serialization;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// Represents a consumer channel for handling message consumption in RabbitMQ.
/// </summary>
/// <remarks>
///     <para>
///     This class extends the <see cref="CarrotChannel" /> class and provides functionality specific to handling message
///     consumption.
///     </para>
/// </remarks>
internal sealed class ConsumerChannel : CarrotChannel, IConsumerChannel
{
    private readonly AsyncLock _consumerLock = new();
    private readonly ILogger _logger;
    private readonly RunningTaskRegistry _runningTaskRegistry;
    private ushort _ackCount;
    private IAckDelivery? _ackDelivery;
    private IDictionary<string, object?>? _arguments;
    private AsyncEventingBasicConsumer? _asyncEventingBasicConsumer;
    private Func<CarrotMessage, Task<DeliveryStatus>>? _consumingAsyncCallback;
    private string? _consumingQueueName;
    private ushort _prefetchCount;

    private ConsumerChannel(
        IConnection connection,
        TimeSpan networkRecoveryInterval,
        IProtocolSerializer protocolSerializer,
        IBasicPropertiesMapper basicPropertiesMapper,
        ILoggerFactory loggerFactory)
        : base(connection, networkRecoveryInterval, protocolSerializer, basicPropertiesMapper, loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<ConsumerChannel>();
        _runningTaskRegistry = new RunningTaskRegistry();
    }

    private bool AutoAck => _ackCount == 0;

    public Core.Common.AsyncEventHandler<EventArgs>? UnregisteredAsync { get; set; }

    public Core.Common.AsyncEventHandler<EventArgs>? RegisteredAsync { get; set; }

    /// <inheritdoc />
    public async Task StopConsumingAsync()
    {
        using var scope = await _consumerLock.LockAsync().ConfigureAwait(false);
        if (_asyncEventingBasicConsumer != null)
        {
            // Cancel consuming after removing eventHandler to avoid unregistered event to be triggered
            _asyncEventingBasicConsumer.UnregisteredAsync -= ConsumerUnregisteredAsync;
            if (Channel is { IsOpen: true })
            {
                // Cancel consuming
                foreach (var consumerTag in _asyncEventingBasicConsumer.ConsumerTags)
                {
                    Logger.LogDebug("Cancel consumer {ConsumerTag}", consumerTag);
                    await Channel.BasicCancelAsync(consumerTag).ConfigureAwait(false);
                }
            }

            _asyncEventingBasicConsumer.ReceivedAsync -= ConsumerMessageReceivedAsync;
            _asyncEventingBasicConsumer.RegisteredAsync -= ConsumerRegisteredAsync;
            _asyncEventingBasicConsumer.ShutdownAsync -= ConsumerShutdownAsync;
        }

        _asyncEventingBasicConsumer = null;
        _consumingAsyncCallback = null;
        await _runningTaskRegistry.CompleteAddingAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task StartConsumingAsync(
        string queueName,
        ushort ackCount,
        ushort prefetchCount,
        Func<CarrotMessage, Task<DeliveryStatus>> consumingAsyncCallback,
        IDictionary<string, object?>? arguments = null)
    {
        using var scope = await _consumerLock.LockAsync().ConfigureAwait(false);

        if (_asyncEventingBasicConsumer != null)
        {
            throw new InvalidOperationException("ConsumerChannel was already started");
        }

        _consumingAsyncCallback = consumingAsyncCallback;
        _consumingQueueName = queueName;
        _ackCount = ackCount;
        _prefetchCount = prefetchCount;
        _arguments = arguments;

        await StartConsumingAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public bool HasConsumer()
    {
        return _asyncEventingBasicConsumer != null;
    }

    /// <inheritdoc />
    public async Task RejectAsync(ulong deliveryTag, bool requeue)
    {
        if (Channel is not null)
        {
            await Channel.BasicRejectAsync(deliveryTag, requeue).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task AckAsync(ulong deliveryTag, bool multiple)
    {
        if (Channel is not null)
        {
            await Channel.BasicAckAsync(deliveryTag, multiple).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Creates a new instance of the <see cref="ConsumerChannel" /> class.
    /// </summary>
    /// <param name="connection">The broker connection associated with the channel.</param>
    /// <param name="networkRecoveryInterval"></param>
    /// <param name="protocolSerializer">The serializer for <see cref="CarrotMessage" />.</param>
    /// <param name="basicPropertiesMapper">Mapper for the messages basic properties.</param>
    /// <param name="loggerFactory">The logger factory used to create loggers for the channel.</param>
    /// <returns>A new instance of the <see cref="ConsumerChannel" /> class.</returns>
    public new static async Task<IConsumerChannel> CreateAsync(
        IConnection connection,
        TimeSpan networkRecoveryInterval,
        IProtocolSerializer protocolSerializer,
        IBasicPropertiesMapper basicPropertiesMapper,
        ILoggerFactory loggerFactory)
    {
        var channel = new ConsumerChannel(connection, networkRecoveryInterval, protocolSerializer, basicPropertiesMapper, loggerFactory);
        await channel.CreateChannelAsync().ConfigureAwait(false);

        return channel;
    }

    protected override async Task EnsureOrRecoverChannelAsync()
    {
        await base.EnsureOrRecoverChannelAsync().ConfigureAwait(false);

        if (_asyncEventingBasicConsumer != null)
        {
            await StartConsumingAsync().ConfigureAwait(false);
        }
    }

    private IAckDelivery CreateAckDelivery()
    {
        return _ackCount switch
        {
            0 => new AutoAckDelivery(_logger),
            1 => new SingleAckDelivery(this),
            _ => new MultiAckDelivery(this, _ackCount) // > 1
        };
    }

    private async Task SetBasicQosAsync()
    {
        if (_prefetchCount <= 0) return;

        uint prefetchSize = 0; // Always 0 --> can be handled via prefetch-count (if big messages are expected -> set lower prefetch count)
        var
            global = false; // NOT SUPPORTED for Quorum queues (True = qos shared across all consumers on the channel. False = qos applied separately to each new consumer on the channel.)

        _logger.LogDebug("BasicQos {PrefetchSize}, {PrefetchCount}, {Global}", prefetchSize, _prefetchCount, global);

        await Channel!.BasicQosAsync(prefetchSize, _prefetchCount, global).ConfigureAwait(false);
    }

    private async Task StartConsumingAsync()
    {
        _ackDelivery = CreateAckDelivery();
        _asyncEventingBasicConsumer = new AsyncEventingBasicConsumer(Channel!);
        _asyncEventingBasicConsumer.ReceivedAsync += ConsumerMessageReceivedAsync;
        _asyncEventingBasicConsumer.RegisteredAsync += ConsumerRegisteredAsync;
        _asyncEventingBasicConsumer.ShutdownAsync += ConsumerShutdownAsync;
        _asyncEventingBasicConsumer.UnregisteredAsync += ConsumerUnregisteredAsync;

        await SetBasicQosAsync().ConfigureAwait(false);
        _logger.LogDebug("Start consuming {QueueName}, {AutoAck}", _consumingQueueName, AutoAck);
        await Channel!.BasicConsumeAsync(_consumingQueueName!, AutoAck, string.Empty, _arguments, _asyncEventingBasicConsumer).ConfigureAwait(false);
    }

    /// <summary>
    /// Use only for testing
    /// </summary>
    internal async Task SendConsumerMessageReceivedEventAsync(BasicDeliverEventArgs ea)
    {
        await ConsumerMessageReceivedAsync(this, ea).ConfigureAwait(false);
    }

    private async Task ConsumerMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        if (_consumingAsyncCallback == null || !_runningTaskRegistry.TryAdd(ea))
        {
            // Do nothing. CarrotService is going to stop the consumer
            // if auto-ack is configured --> message is lost
            return;
        }

        DeliveryStatus deliveryStatus = DeliveryStatus.Reject;
        try
        {
            deliveryStatus = await _consumingAsyncCallback(DeserializeMessage(ea)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unhandled exception while consuming message {MessageId}; Exchange:{Exchange}, RoutingKey:{RoutingKey}, DeliveryTag:{DeliveryTag}, ConsumerTag:{ConsumerTag}",
                ea.BasicProperties.MessageId,
                ea.Exchange,
                ea.RoutingKey,
                ea.DeliveryTag,
                ea.ConsumerTag);
        }

        try
        {
            await _ackDelivery!.DeliverAsync(ea.DeliveryTag, deliveryStatus).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unhandled exception while delivering acknowledgement for message {MessageId}; Exchange:{Exchange}, RoutingKey:{RoutingKey}, DeliveryTag:{DeliveryTag}, ConsumerTag:{ConsumerTag}",
                ea.BasicProperties.MessageId,
                ea.Exchange,
                ea.RoutingKey,
                ea.DeliveryTag,
                ea.ConsumerTag);
        }

        _runningTaskRegistry.Remove(ea);
    }

    private CarrotMessage DeserializeMessage(BasicDeliverEventArgs ea)
    {
#if NET
        string payload = Encoding.UTF8.GetString(ea.Body.Span);
#else
        var payload = Encoding.UTF8.GetString(ea.Body.ToArray());
#endif
        _logger.LogDebug("Consuming {Payload} ...", payload);

        return ProtocolSerializer.Deserialize(payload, ea.BasicProperties);
    }

    private Task ConsumerUnregisteredAsync(object sender, ConsumerEventArgs e)
    {
        Logger.LogDebug("AsyncEventingBasicConsumer.UnregisteredAsync {ConsumerTag}", string.Join(",", e.ConsumerTags));

        if (UnregisteredAsync != null)
        {
            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await UnregisteredAsync.InvokeAllAsync(this, EventArgs.Empty).ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        Logger.LogError(exception, $"Error while calling {nameof(UnregisteredAsync)} from {nameof(ConsumerChannel)}");
                    }
                });
        }

        return Task.CompletedTask;
    }

    private Task ConsumerRegisteredAsync(object sender, ConsumerEventArgs e)
    {
        Logger.LogDebug("AsyncEventingBasicConsumer.RegisteredAsync {ConsumerTag}", string.Join(",", e.ConsumerTags));
        if (RegisteredAsync != null)
        {
            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await RegisteredAsync.InvokeAllAsync(this, EventArgs.Empty).ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        Logger.LogError(exception, $"Error while calling {nameof(RegisteredAsync)} from {nameof(ConsumerChannel)}");
                    }
                });
        }

        return Task.CompletedTask;
    }

    private Task ConsumerShutdownAsync(object sender, ShutdownEventArgs e)
    {
        Logger.LogDebug("AsyncEventingBasicConsumer.ShutdownAsync {ReplyCode}, {ReplyText}", e.ReplyCode, e.ReplyText);

        return Task.CompletedTask;
    }

    protected override Task DisposeChannelAsync()
    {
        _ackDelivery?.Dispose();
        _ackDelivery = null;

        return base.DisposeChannelAsync();
    }
}