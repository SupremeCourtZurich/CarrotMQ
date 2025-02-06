using System;
using System.Threading.Tasks;
using CarrotMQ.Core.Common;
using CarrotMQ.Core.Protocol;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// The abstraction of the rabbitmq broker connection.
/// </summary>
public interface IBrokerConnection : IAsyncDisposable
{
    /// <summary>
    /// Service name associated with the broker connection.
    /// </summary>
    /// <remarks>Used to set <see cref="CarrotHeader.ServiceName" /></remarks>
    string ServiceName { get; }

    /// <summary>
    /// Unique ID identifying this service instance
    /// </summary>
    public Guid ServiceInstanceId { get; }

    /// <summary>
    /// RabbitMQ VHost
    /// </summary>
    string VHost { get; }

    /// <summary>
    /// Username used for the authentication with the broker
    /// </summary>
    string UserName { get; }

    /// <summary>
    /// True if the connection is blocked (tcp backpressure).
    /// </summary>
    bool ConnectionIsBlocked { get; }

    /// <summary>
    /// Amount of time client will wait for before re-trying  to recover connection.
    /// </summary>
    TimeSpan NetworkRecoveryInterval { get; }

    /// <summary>
    /// Event triggered before brokerConnection is closed
    /// </summary>
    event AsyncEventHandler<EventArgs>? ConnectionClosing;

    /// <summary>
    /// Opens the connection to RabbitMQ and returns the underlying connection object
    /// </summary>
    /// <returns>The underlying connection to the RabbitMQ broker.</returns>
    /// <remarks>
    /// The first call creates the connection.
    /// This method waits until the RabbitMQ connection is open. If the connection is not open,
    /// it sleeps for the specified network recovery interval plus an additional delay before checking again.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown if the broker connection has been disposed.</exception>
    /// <exception cref="BrokerUnreachableException">Thrown when the broker is unreachable, and the connection cannot be established.</exception>
    Task<IConnection> ConnectAsync();

    /// <summary>
    /// Gets the publisher channel without confirms.
    /// </summary>
    Task<IPublisherChannel> GetPublisherChannelAsync();

    /// <summary>
    /// Gets the publisher channel with confirms.
    /// </summary>
    Task<IPublisherChannel> GetPublisherChannelWithConfirmsAsync();

    /// <summary>
    /// Gets the direct reply channel.
    /// </summary>
    Task<IDirectReplyChannel> GetDirectReplyChannelAsync();

    /// <summary>
    /// Gets the direct reply channel with confirms.
    /// </summary>
    Task<IDirectReplyChannel> GetDirectReplyConfirmChannelAsync();

    /// <summary>
    /// Gets the consumer channel.
    /// </summary>
    /// <remarks>The caller is responsible for disponsing the returned <see cref="IConsumerChannel" /></remarks>
    Task<IConsumerChannel> CreateConsumerChannelAsync();

    /// <summary>
    /// Closes the current connection to rabbitMq
    /// </summary>
    Task CloseAsync();

    /// <summary>
    /// Event raised when a transport error is received from a channel.
    /// </summary>
    /// <remarks>
    /// This event is triggered when a transport error is received from one of the RabbitMQ channels.
    /// It provides information about the error through the <see cref="TransportErrorReceivedEventArgs" />.
    /// </remarks>
    public event EventHandler<TransportErrorReceivedEventArgs> TransportErrorReceived;
}