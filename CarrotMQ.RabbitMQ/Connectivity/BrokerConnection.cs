using System;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Common;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// Represents a connection to a RabbitMQ broker.
/// </summary>
public sealed class BrokerConnection : IBrokerConnection
{
    private readonly BrokerConnectionOptions _brokerConnectionOptions;
    private readonly AsyncLock _channelsLock = new();
    private readonly ConnectionFactory _connectionFactory;
    private readonly AsyncLock _connectionLock = new();
    private readonly string _displayConnectionName;
    private readonly IEndpointResolver _endpointResolver;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IProtocolSerializer _protocolSerializer;
    private IConnection? _connection;
    private IDirectReplyChannel? _directReplyChannel;
    private IDirectReplyChannel? _directReplyConfirmChannel;
    private bool _disposed;
    private IPublisherChannel? _publisherChannelWithConfirms;
    private IPublisherChannel? _publisherChannelWithoutConfirms;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrokerConnection" /> class.
    /// </summary>
    /// <param name="brokerConnectionOptions">Options for configuring the broker connection.</param>
    /// <param name="endpointResolver">The resolver for RabbitMQ endpoints.</param>
    /// <param name="protocolSerializer">The serializer for <see cref="CarrotMessage" />.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public BrokerConnection(
        IOptions<BrokerConnectionOptions> brokerConnectionOptions,
        IEndpointResolver endpointResolver,
        IProtocolSerializer protocolSerializer,
        ILogger<BrokerConnection> logger,
        ILoggerFactory loggerFactory)
    {
        _brokerConnectionOptions = brokerConnectionOptions.Value;

        _displayConnectionName = !string.IsNullOrEmpty(_brokerConnectionOptions.DisplayConnectionName)
            ? _brokerConnectionOptions.DisplayConnectionName!
            : $"{ServiceName} {ServiceInstanceId}";

        _endpointResolver = endpointResolver;
        _protocolSerializer = protocolSerializer;
        _logger = logger;
        _loggerFactory = loggerFactory;

        _connectionFactory = CreateConnectionFactory();
    }

    /// <inheritdoc />
    public string UserName => _brokerConnectionOptions.UserName;

    /// <inheritdoc />
    public string ServiceName => _brokerConnectionOptions.ServiceName;

    /// <inheritdoc />
    public Guid ServiceInstanceId => _brokerConnectionOptions.ServiceInstanceId;

    /// <inheritdoc />
    public string VHost => _brokerConnectionOptions.VHost;

    /// <inheritdoc />
    public bool ConnectionIsBlocked { get; private set; }

    /// <inheritdoc />
    public TimeSpan NetworkRecoveryInterval { get; private set; }

    /// <inheritdoc />
    public event Core.Common.AsyncEventHandler<EventArgs>? ConnectionClosing;

    /// <inheritdoc />
    public async Task<IConnection> ConnectAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(BrokerConnection));

        if (_connection == null) await ConnectInternallyAsync().ConfigureAwait(false);

        while (!_connection!.IsOpen)
        {
            _logger.LogDebug("Wait for connection open");
            await Task.Delay(_connectionFactory.NetworkRecoveryInterval.Add(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
        }

        return _connection;
    }

    private async Task ConnectInternallyAsync()
    {
        using var scope = await _connectionLock.LockAsync().ConfigureAwait(false);

        if (_connection != null) return;

        using CancellationTokenSource cts = new(_brokerConnectionOptions.InitialConnectionTimeout);

        while (true)
        {
            try
            {
                _connection = await _connectionFactory.CreateConnectionAsync(_endpointResolver, _displayConnectionName, cts.Token)
                    .ConfigureAwait(false);
                _connection.ConnectionShutdownAsync += SharedConnectionShutdownHandlerAsync;
                _connection.ConnectionBlockedAsync += SharedConnectionConnectionBlockedHandlerAsync;
                _connection.ConnectionUnblockedAsync += SharedConnectionConnectionUnblockedHandlerAsync;
                _connection.CallbackExceptionAsync += SharedConnectionCallbackExceptionAsync;

                _connection.RecoverySucceededAsync += AutoRecoveringConnectionOnRecoverySucceededAsync;
                _connection.ConnectionRecoveryErrorAsync += AutoRecoveringConnectionOnConnectionRecoveryErrorAsync;

                _connection.ConsumerTagChangeAfterRecoveryAsync += ConnectionOnConsumerTagChangeAfterRecoveryAsync;
                _connection.QueueNameChangedAfterRecoveryAsync += ConnectionOnQueueNameChangedAfterRecoveryAsync;
                _connection.RecoveringConsumerAsync += ConnectionOnRecoveringConsumerAsync;

                _logger.LogInformation(
                    "{ServiceName}/{ServiceInstanceId} connected to {ConnectedNode}/{VHost})",
                    ServiceName,
                    ServiceInstanceId,
                    _connection.Endpoint,
                    VHost);

                return;
            }
            catch (BrokerUnreachableException ex)
            {
                _logger.LogError(ex, "Could not create a broker connection! Check your endpoint configuration (node, user, password etc.)");
                if (cts.Token.IsCancellationRequested)
                {
                    throw;
                }
            }

            await Task.Delay(_connectionFactory.NetworkRecoveryInterval).ConfigureAwait(false);
        }
    }

    private ConnectionFactory CreateConnectionFactory()
    {
        var connectionFactory = new ConnectionFactory
        {
            VirtualHost = _brokerConnectionOptions.VHost,
            UserName = _brokerConnectionOptions.UserName,
            Password = _brokerConnectionOptions.Password,
            ClientProperties = _brokerConnectionOptions.ClientProperties
        };

        if (_brokerConnectionOptions.ConsumerDispatchConcurrency > 0)
        {
            connectionFactory.ConsumerDispatchConcurrency = _brokerConnectionOptions.ConsumerDispatchConcurrency;
        }

        _brokerConnectionOptions.ConfigureConnectionFactory?.Invoke(connectionFactory);

        connectionFactory.RequestedConnectionTimeout =
            _brokerConnectionOptions.InitialConnectionTimeout < connectionFactory.RequestedConnectionTimeout
                ? _brokerConnectionOptions.InitialConnectionTimeout
                : connectionFactory.RequestedConnectionTimeout;

        NetworkRecoveryInterval = connectionFactory.NetworkRecoveryInterval;

        return connectionFactory;
    }

    /// <inheritdoc />
    public async Task<IPublisherChannel> GetPublisherChannelAsync()
    {
        if (_publisherChannelWithoutConfirms != null) return _publisherChannelWithoutConfirms; // Check outside of lock for performance reason

        using var scope = await _channelsLock.LockAsync().ConfigureAwait(false);

        if (_publisherChannelWithoutConfirms != null) return _publisherChannelWithoutConfirms;

        var connection = await ConnectAsync().ConfigureAwait(false);
        _publisherChannelWithoutConfirms =
            await PublisherChannel.CreateAsync(connection, NetworkRecoveryInterval, _protocolSerializer, _loggerFactory)
                .ConfigureAwait(false);

        _publisherChannelWithoutConfirms.TransportErrorReceived += OnTransportErrorReceivedFromChannel;

        return _publisherChannelWithoutConfirms;
    }

    /// <inheritdoc />
    public async Task<IPublisherChannel> GetPublisherChannelWithConfirmsAsync()
    {
        if (_publisherChannelWithConfirms != null) return _publisherChannelWithConfirms; // Check outside of lock for performance reason

        using var scope = await _channelsLock.LockAsync().ConfigureAwait(false);

        if (_publisherChannelWithConfirms != null) return _publisherChannelWithConfirms;

        var connection = await ConnectAsync().ConfigureAwait(false);
        _publisherChannelWithConfirms = await PublisherConfirmChannel.CreateAsync(
                connection,
                NetworkRecoveryInterval,
                _brokerConnectionOptions.PublisherConfirm,
                _protocolSerializer,
                _loggerFactory)
            .ConfigureAwait(false);

        _publisherChannelWithConfirms.TransportErrorReceived += OnTransportErrorReceivedFromChannel;

        return _publisherChannelWithConfirms;
    }

    /// <inheritdoc />
    public async Task<IDirectReplyChannel> GetDirectReplyChannelAsync()
    {
        if (_directReplyChannel != null) return _directReplyChannel; // Check outside of lock for performance reason

        using var scope = await _channelsLock.LockAsync().ConfigureAwait(false);

        if (_directReplyChannel != null) return _directReplyChannel;

        var connection = await ConnectAsync().ConfigureAwait(false);
        _directReplyChannel = await DirectReplyChannel.CreateAsync(
                connection,
                NetworkRecoveryInterval,
                _protocolSerializer,
                _loggerFactory)
            .ConfigureAwait(false);

        _directReplyChannel.TransportErrorReceived += OnTransportErrorReceivedFromChannel;

        return _directReplyChannel;
    }

    /// <inheritdoc />
    public async Task<IDirectReplyChannel> GetDirectReplyConfirmChannelAsync()
    {
        if (_directReplyConfirmChannel != null) return _directReplyConfirmChannel; // Check outside of lock for performance reason

        using var scope = await _channelsLock.LockAsync().ConfigureAwait(false);

        if (_directReplyConfirmChannel != null) return _directReplyConfirmChannel;

        var connection = await ConnectAsync().ConfigureAwait(false);
        _directReplyConfirmChannel =
            await DirectReplyConfirmChannel.CreateAsync(
                    connection,
                    NetworkRecoveryInterval,
                    _brokerConnectionOptions.PublisherConfirm,
                    _protocolSerializer,
                    _loggerFactory)
                .ConfigureAwait(false);

        _directReplyConfirmChannel.TransportErrorReceived += OnTransportErrorReceivedFromChannel;

        return _directReplyConfirmChannel;
    }

    /// <inheritdoc />
    public async Task<IConsumerChannel> CreateConsumerChannelAsync()
    {
        var connection = await ConnectAsync().ConfigureAwait(false);
        var channel = await ConsumerChannel.CreateAsync(
                connection,
                NetworkRecoveryInterval,
                _protocolSerializer,
                _loggerFactory)
            .ConfigureAwait(false);
        channel.TransportErrorReceived += OnTransportErrorReceivedFromChannel;

        return channel;
    }

    private void OnTransportErrorReceivedFromChannel(object? sender, TransportErrorReceivedEventArgs e)
    {
        TransportErrorReceived?.Invoke(sender, e);
    }

    /// <summary>
    /// Disposes of the broker connection, releasing associated resources.
    /// </summary>
    /// <remarks>
    /// This method disposes of the RabbitMQ channels and the connection. It also unregisters event handlers
    /// from the RabbitMQ connection events.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        _logger.LogDebug("Disposing...");
        if (!_disposed)
        {
            await CloseAsync().ConfigureAwait(false);

            _disposed = true;
        }

        _logger.LogDebug("Disposed");
    }

    /// <inheritdoc />
    public async Task CloseAsync()
    {
        using var channelLockScope = await _channelsLock.LockAsync().ConfigureAwait(false);
        using var scope = await _connectionLock.LockAsync().ConfigureAwait(false);

        if (_connection != null)
        {
            if (ConnectionClosing != null)
            {
                await ConnectionClosing.InvokeAllAsync(this, EventArgs.Empty).ConfigureAwait(false);
            }

            if (_publisherChannelWithConfirms != null) await _publisherChannelWithConfirms.DisposeAsync().ConfigureAwait(false);
            if (_publisherChannelWithoutConfirms != null) await _publisherChannelWithoutConfirms.DisposeAsync().ConfigureAwait(false);
            if (_directReplyChannel != null) await _directReplyChannel.DisposeAsync().ConfigureAwait(false);
            if (_directReplyConfirmChannel != null) await _directReplyConfirmChannel.DisposeAsync().ConfigureAwait(false);

            _connection.ConnectionShutdownAsync -= SharedConnectionShutdownHandlerAsync;
            _connection.ConnectionBlockedAsync -= SharedConnectionConnectionBlockedHandlerAsync;
            _connection.ConnectionUnblockedAsync -= SharedConnectionConnectionUnblockedHandlerAsync;
            _connection.CallbackExceptionAsync -= SharedConnectionCallbackExceptionAsync;
            _connection.RecoverySucceededAsync -= AutoRecoveringConnectionOnRecoverySucceededAsync;
            _connection.ConnectionRecoveryErrorAsync -= AutoRecoveringConnectionOnConnectionRecoveryErrorAsync;

            _connection.ConsumerTagChangeAfterRecoveryAsync -= ConnectionOnConsumerTagChangeAfterRecoveryAsync;
            _connection.QueueNameChangedAfterRecoveryAsync -= ConnectionOnQueueNameChangedAfterRecoveryAsync;
            _connection.RecoveringConsumerAsync -= ConnectionOnRecoveringConsumerAsync;

            _connection.Dispose();

            _connection = null;
            _publisherChannelWithConfirms = null;
            _publisherChannelWithoutConfirms = null;
            _directReplyChannel = null;
            _directReplyConfirmChannel = null;
        }
    }

    /// <inheritdoc />
    public event EventHandler<TransportErrorReceivedEventArgs>? TransportErrorReceived;

    /// <summary>
    /// Event handler for RabbitMQ connection callback exceptions.
    /// </summary>
    /// <param name="sender">The object that raised the event.</param>
    /// <param name="ex">The <see cref="CallbackExceptionEventArgs" /> containing information about the callback exception.</param>
    /// <remarks>
    /// This method is invoked when a callback exception occurs on the RabbitMQ connection.
    /// Callback exceptions typically represent unexpected situations, such as protocol-level errors.
    /// The method logs the exception details for debugging purposes.
    /// </remarks>
    private Task SharedConnectionCallbackExceptionAsync(object sender, CallbackExceptionEventArgs ex)
    {
        _logger.LogDebug(ex.Exception, "ConnectionRecoveryError received!");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Event handler for RabbitMQ connection shutdown events.
    /// </summary>
    /// <param name="sender">The object that raised the event.</param>
    /// <param name="ex">The <see cref="ShutdownEventArgs" /> containing information about the shutdown event.</param>
    /// <remarks>
    /// This method is invoked when the RabbitMQ connection is shut down.
    /// It logs details about the shutdown event, including the cause, initiator, reply code, and reply text.
    /// In cases where the shutdown is expected (e.g., initiated by the application or RabbitMQ management plugin),
    /// the method avoids logging certain reply codes to prevent unnecessary log entries.
    /// </remarks>
    private Task SharedConnectionShutdownHandlerAsync(object sender, ShutdownEventArgs ex)
    {
        _logger.LogWarning(
            "Connection shut down.\n Cause: {Cause}\n Initiator: {Initiator}\n ReplyCode: {ReplyCode}, ReplyText: {ReplyText}\n AutomaticRecoveryEnabled: {AutomaticRecoveryEnabled}",
            ex.Cause,
            ex.Initiator,
            ex.ReplyCode,
            ex.ReplyText,
            _connectionFactory.AutomaticRecoveryEnabled);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Event handler for the RabbitMQ connection when it is unblocked.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="asyncEventArgs">The <see cref="AsyncEventArgs" /> containing event data.</param>
    /// <remarks>
    /// This handler is invoked when the RabbitMQ connection is unblocked.
    /// It sets the <see cref="ConnectionIsBlocked" /> property to <see langword="false" />.
    /// </remarks>
    private Task SharedConnectionConnectionUnblockedHandlerAsync(object sender, AsyncEventArgs asyncEventArgs)
    {
        ConnectionIsBlocked = false;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Event handler for the RabbitMQ connection when it is blocked.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The <see cref="ConnectionBlockedEventArgs" /> containing event data.</param>
    /// <remarks>
    /// This handler is invoked when the RabbitMQ connection is blocked.
    /// It sets the <see cref="ConnectionIsBlocked" /> property to <see langword="true" />.
    /// </remarks>
    private Task SharedConnectionConnectionBlockedHandlerAsync(object sender, ConnectionBlockedEventArgs e)
    {
        ConnectionIsBlocked = true;

        return Task.CompletedTask;
    }

    private Task AutoRecoveringConnectionOnConnectionRecoveryErrorAsync(object sender, ConnectionRecoveryErrorEventArgs e)
    {
        _logger.LogWarning(e.Exception, "Error on auto recovering of brokerConnection");

        return Task.CompletedTask;
    }

    private Task AutoRecoveringConnectionOnRecoverySucceededAsync(object sender, AsyncEventArgs asyncEventArgs)
    {
        _logger.LogDebug("AutoRecovering connection succeeded");

        return Task.CompletedTask;
    }

    private Task ConnectionOnRecoveringConsumerAsync(object sender, RecoveringConsumerEventArgs @event)
    {
        _logger.LogDebug("IConnection.RecoveringConsumerAsync:  ConsumerTag:{ConsumerTag}", @event.ConsumerTag);

        return Task.CompletedTask;
    }

    private Task ConnectionOnQueueNameChangedAfterRecoveryAsync(object sender, QueueNameChangedAfterRecoveryEventArgs @event)
    {
        _logger.LogDebug(
            "IConnection.QueueNameChangedAfterRecoveryAsync:  NameBefore:{NameBefore} NameAfter:{NameAfter}",
            @event.NameBefore,
            @event.NameAfter);

        return Task.CompletedTask;
    }

    private Task ConnectionOnConsumerTagChangeAfterRecoveryAsync(object sender, ConsumerTagChangedAfterRecoveryEventArgs @event)
    {
        _logger.LogDebug(
            "IConnection.ConsumerTagChangeAfterRecoveryAsync:  TagBefore:{TagBefore} TagAfter:{TagAfter}",
            @event.TagBefore,
            @event.TagAfter);

        return Task.CompletedTask;
    }
}