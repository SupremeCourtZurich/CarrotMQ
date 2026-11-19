using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Common;
using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Configuration.Exchanges;
using CarrotMQ.RabbitMQ.Configuration.Queues;
using CarrotMQ.RabbitMQ.Serialization;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <inheritdoc />
internal class CarrotChannel : ICarrotChannel
{
    private readonly TimeSpan _networkRecoveryInterval;

    protected CarrotChannel(
        IConnection connection,
        TimeSpan networkRecoveryInterval,
        IProtocolSerializer protocolSerializer,
        ILoggerFactory loggerFactory)
    {
        _networkRecoveryInterval = networkRecoveryInterval;
        Connection = connection;
        ProtocolSerializer = protocolSerializer;
        Logger = loggerFactory.CreateLogger<CarrotChannel>();
    }

    private TaskCompletionSource<bool> _reopened = CompletedSignal();
    private bool _subscribedToRecovery;
    private int _recoveryRunning;

    /// <summary>
    /// Completes when the channel may be open again: CarrotMQ reopened it, the client recovered the connection, or the
    /// channel was disposed.
    /// Call under <see cref="ChannelLock" />, then check <see cref="IsOpen" /> again before waiting.
    /// </summary>
    protected Task NextReopenAsync()
    {
        if (Volatile.Read(ref _reopened).Task.IsCompleted)
        {
            Volatile.Write(ref _reopened, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        }

        return Volatile.Read(ref _reopened).Task;
    }

    private static TaskCompletionSource<bool> CompletedSignal()
    {
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult(true);

        return signal;
    }

    private void SignalReopened()
    {
        Volatile.Read(ref _reopened).TrySetResult(true);
    }

    private Task ConnectionRecoverySucceededAsync(object sender, AsyncEventArgs e)
    {
        SignalReopened();

        return Task.CompletedTask;
    }

    protected IProtocolSerializer ProtocolSerializer { get; }

    protected AsyncLock ChannelLock { get; } = new();

    protected ILogger Logger { get; }

    /// <summary>
    /// The reason of the last shutdown of <see cref="Channel" />, or <c>null</c> before the first one.
    /// </summary>
    protected ShutdownEventArgs? LastShutdownReason { get; private set; }

    /// <summary>
    /// The underlying RabbitMQ channel for communication.
    /// </summary>
    protected IChannel? Channel { get; set; }

    public IConnection Connection { get; }

    /// <inheritdoc />
    public bool IsOpen => Channel is { IsOpen: true };

    /// <inheritdoc />
    public bool IsClosed => !IsOpen;

    /// <summary>
    /// Used as IsDisposed AND IsDisposing
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc cref="ICarrotChannel.TransportErrorReceived" />
    public event EventHandler<TransportErrorReceivedEventArgs>? TransportErrorReceived;

    protected void OnTransportErrorReceived(TransportErrorReceivedEventArgs e)
    {
        TransportErrorReceived?.Invoke(this, e);
    }

    /// <summary>
    /// Acquires <see cref="ChannelLock" /> once the channel is open, waiting for a reopen while it is closed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The channel was disposed before it was open again.</exception>
    protected async Task<IDisposable> LockOpenChannelAsync(CancellationToken token = default)
    {
        while (true)
        {
            var scope = await ChannelLock.LockAsync(token).ConfigureAwait(false);
            if (IsDisposed)
            {
                scope.Dispose();
                throw new ObjectDisposedException(nameof(CarrotChannel));
            }

            if (IsOpen) return scope;

            var reopened = NextReopenAsync();
            if (IsOpen) return scope;

            scope.Dispose();
            await CancellableWait.WaitAsync(reopened, token).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public virtual async ValueTask DisposeAsync()
    {
        using var scope = await ChannelLock.LockAsync().ConfigureAwait(false);
        Logger.LogDebug("Channel {ChannelNumber} is being disposed", Channel?.ChannelNumber);

        if (IsDisposed) throw new ObjectDisposedException(nameof(CarrotChannel));
        IsDisposed = true;
        Connection.RecoverySucceededAsync -= ConnectionRecoverySucceededAsync;
        SignalReopened();

        await DisposeChannelAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async Task DeclareQueueAsync(
        string queueName,
        bool durable,
        bool exclusive,
        bool autoDelete,
        IDictionary<string, object?>? arguments = null)
    {
        Logger.LogInformation(
            "Declaring queue {QueueName} durable:{Durable} exclusive:{Exclusive} autodelete:{Autodelete}",
            queueName,
            durable,
            exclusive,
            autoDelete);

        using var scope = await LockOpenChannelAsync().ConfigureAwait(false);

        var declareOk = await Channel!.QueueDeclareAsync(queueName, durable, exclusive, autoDelete, arguments).ConfigureAwait(false);

        Logger.LogInformation("Queue {QueueName} declared.", declareOk.QueueName);
    }

    /// <inheritdoc />
    public async Task ApplyConfigurations(QueueConfiguration queueConfig, IList<BindingConfiguration> bindings)
    {
        if (queueConfig.DeclareQueue)
        {
            await DeclareQueueAsync(queueConfig.QueueName, queueConfig.Durable, queueConfig.Exclusive, queueConfig.AutoDelete, queueConfig.Arguments)
                .ConfigureAwait(false);
        }

        foreach (var binding in bindings)
        {
            await BindQueueAsync(queueConfig.QueueName, binding.Exchange, binding.RoutingKey).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task DeleteQueueAsync(string queueName)
    {
        Logger.LogInformation("Delete queue {QueueName}", queueName);

        using var scope = await LockOpenChannelAsync().ConfigureAwait(false);
        await Channel!.QueueDeleteAsync(queueName).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeclareExchangeAsync(
        string exchangeName,
        string exchangeType,
        bool durable,
        bool autoDelete,
        IDictionary<string, object?>? arguments = null)
    {
        Logger.LogInformation("Declaring exchange {ExchangeName}", exchangeName);

        using var scope = await LockOpenChannelAsync().ConfigureAwait(false);
        await Channel!.ExchangeDeclareAsync(exchangeName, exchangeType, durable, autoDelete, arguments).ConfigureAwait(false);
    }

    public async Task DeclareExchangesAsync(ExchangeCollection exchangeCollection)
    {
        foreach (var exchange in exchangeCollection.GetExchangeConfigurations())
        {
            await DeclareExchangeAsync(exchange.Name, exchange.Type, exchange.Durable, exchange.AutoDelete, exchange.Arguments).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task DeleteExchangeAsync(string exchangeName, bool ifUnused = false)
    {
        Logger.LogInformation("Delete exchange {ExchangeName}", exchangeName);

        using var scope = await LockOpenChannelAsync().ConfigureAwait(false);
        await Channel!.ExchangeDeleteAsync(exchangeName, ifUnused).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task BindQueueAsync(
        string queueName,
        string exchange,
        string routingKey,
        IDictionary<string, object?>? arguments = null)
    {
        Logger.LogInformation(
            "Declaring binding from exchange {Exchange} to queue {QueueName} with bindingKey {RoutingKey}",
            exchange,
            queueName,
            routingKey);

        using var scope = await LockOpenChannelAsync().ConfigureAwait(false);
        await Channel!.QueueBindAsync(queueName, exchange, routingKey, arguments).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> CheckQueueAsync(string queueName)
    {
        Logger.LogDebug("Checking queue {QueueName}", queueName);

        var queueExists = await CheckEntityAsync(async () => await Channel!.QueueDeclarePassiveAsync(queueName).ConfigureAwait(false))
            .ConfigureAwait(false);

        Logger.LogDebug("Queue {QueueName} exists = {QueueExists}", queueName, queueExists);

        return queueExists;
    }

    /// <inheritdoc />
    public async Task<bool> CheckExchangeAsync(string exchangeName)
    {
        Logger.LogDebug("check exchange {ExchangeName}", exchangeName);

        var exchangeExists = await CheckEntityAsync(async () => await Channel!.ExchangeDeclarePassiveAsync(exchangeName).ConfigureAwait(false))
            .ConfigureAwait(false);

        Logger.LogDebug("Exchange {ExchangeName} exists = {ExchangeExists}", exchangeName, exchangeExists);

        return exchangeExists;
    }

    /// <summary>
    /// Creates a new instance of <see cref="ICarrotChannel" /> associated with the provided <paramref name="connection" />.
    /// </summary>
    /// <param name="connection">The <see cref="IBrokerConnection" /> associated with the channel.</param>
    /// <param name="networkRecoveryInterval"></param>
    /// <param name="protocolSerializer">The serializer for <see cref="CarrotMessage" />.</param>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory" /> used for creating loggers.</param>
    /// <returns>A new instance of <see cref="ICarrotChannel" />.</returns>
    public static async Task<ICarrotChannel> CreateAsync(
        IConnection connection,
        TimeSpan networkRecoveryInterval,
        IProtocolSerializer protocolSerializer,
        ILoggerFactory loggerFactory)
    {
        var channel = new CarrotChannel(connection, networkRecoveryInterval, protocolSerializer, loggerFactory);
        await channel.CreateChannelAsync().ConfigureAwait(false);

        return channel;
    }

    protected virtual async Task CreateChannelAsync()
    {
        Debug.Assert(Channel == null);
        Debug.Assert(!IsDisposed);

        Logger.LogDebug("CreateAsync channel");
        Channel = await Connection.CreateChannelAsync(CreateChannelOptions()).ConfigureAwait(false);
        Channel.ChannelShutdownAsync += ChannelShutdownAsync;
        Channel.FlowControlAsync += ChannelOnFlowControlAsync;
        Channel.CallbackExceptionAsync += ChannelOnCallbackExceptionAsync;
        if (!_subscribedToRecovery)
        {
            Connection.RecoverySucceededAsync += ConnectionRecoverySucceededAsync;
            _subscribedToRecovery = true;
        }

        SignalReopened();

        Logger.LogDebug("Channel {ChannelNumber} has been created", Channel?.ChannelNumber);
    }

    protected virtual CreateChannelOptions CreateChannelOptions()
    {
        return new CreateChannelOptions(false, false, consumerDispatchConcurrency: null);
    }

    private Task ChannelOnCallbackExceptionAsync(object sender, CallbackExceptionEventArgs e)
    {
        Logger.LogDebug(e.Exception, "IChannel.CallbackExceptionAsync");

        return Task.CompletedTask;
    }

    private Task ChannelOnFlowControlAsync(object sender, FlowControlEventArgs e)
    {
        Logger.LogDebug("IChannel.FlowControlAsync IsActive={IsActive}", e.Active);

        return Task.CompletedTask;
    }

    protected virtual async Task DisposeChannelAsync()
    {
        Logger.LogDebug("Dispose AMQP channel");

        if (Channel == null) return;

        await Channel.DisposeAsync().ConfigureAwait(false);
        Channel = null;
    }

    private Task ChannelShutdownAsync(object sender, ShutdownEventArgs shutdownEventArgs)
    {
        if (IsDisposed) return Task.CompletedTask;

        Logger.LogInformation("Channel shutdown. Reason: {ReplyCode}:{ReplyText}", shutdownEventArgs.ReplyCode, shutdownEventArgs.ReplyText);
        LastShutdownReason = shutdownEventArgs;

        OnTransportErrorReceived(
            new TransportErrorReceivedEventArgs { ErrorReason = TransportErrorReceivedEventArgs.TransportErrorReason.ChannelInterrupted });

        if (shutdownEventArgs.ReplyCode < 400) return Task.CompletedTask; // recovered by RabbitMQ

        // AMQP exceptions:
        // https://www.rabbitmq.com/amqp-0-9-1-reference.html
        // 403 access-refused The client attempted to work with a server entity to which it has no access due to security settings.
        // 404 not-found The client attempted to work with a server entity that does not exist.
        // 405 resource-locked The client attempted to work with a server entity to which it has no access because another client is working with it.
        //     Accessing an exclusive queue from a connection other than its declaring one
        // 406 precondition-failed The client requested a method that was not allowed because some precondition failed.
        //     Declaring an existing queue/exchange with non-matching properties

#pragma warning disable MA0134
        if (Interlocked.CompareExchange(ref _recoveryRunning, 1, 0) != 0) return Task.CompletedTask;

        Task.Run(async () =>
        {
            // The lock is released between attempts, so dispose and waiting publishes are not blocked by an outage.
            while (true)
            {
                using (await ChannelLock.LockAsync().ConfigureAwait(false))
                {
                    if (IsDisposed || !IsClosed)
                    {
                        Volatile.Write(ref _recoveryRunning, 0);

                        // A close between the check and the reset started no loop: this one takes it over.
                        if (IsDisposed || !IsClosed || Interlocked.CompareExchange(ref _recoveryRunning, 1, 0) != 0) return;

                        continue;
                    }

                    try
                    {
                        await EnsureOrRecoverChannelAsync().ConfigureAwait(false);

                        continue;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "AMQP channel recovery failed -> retry");
                    }
                }

                await Task.Delay(_networkRecoveryInterval).ConfigureAwait(false); // Avoid floading RabbitMQ when something is wrong
            }
        });
#pragma warning restore MA0134

        return Task.CompletedTask;
    }

    protected virtual async Task EnsureOrRecoverChannelAsync()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(CarrotChannel));

        if (IsOpen) return;

        Logger.LogDebug("Recovering channel...");

        await DisposeChannelAsync().ConfigureAwait(false);
        await CreateChannelAsync().ConfigureAwait(false);

        Logger.LogDebug("Channel recovered");
    }

    private async Task<bool> CheckEntityAsync(Func<Task> checkEntity)
    {
        var entityExists = false;
        try
        {
            using var scope = await LockOpenChannelAsync().ConfigureAwait(false);
            await checkEntity().ConfigureAwait(false);

            entityExists = true;
        }
        catch (OperationInterruptedException e)
        {
            // channel is closed -> ChannelShutdown event
            // throw all except 404 NOT_FOUND = entity does not exist
            if (e.ShutdownReason?.ReplyCode != 404)
            {
                Logger.LogDebug(e, "Could not check entity");

                throw;
            }
        }

        return entityExists;
    }
}