using System;
using System.Collections.Generic;
using System.Threading;
using CarrotMQ.Core.Protocol;
using RabbitMQ.Client;

namespace CarrotMQ.RabbitMQ.Configuration;

/// <summary>
/// Represents options for configuring a connection to a RabbitMQ broker.
/// </summary>
public sealed class BrokerConnectionOptions
{
    /// <summary>
    /// Default section name of this option in configuration
    /// </summary>
    public const string BrokerConnection = nameof(BrokerConnection);

    /// <summary>
    /// An array to RabbitMQ nodes in the cluster.
    /// </summary>
    public IList<Uri> BrokerEndPoints { get; set; } = new List<Uri>();

    /// <summary>
    /// If true an endpoint from <see cref="BrokerEndPoints" /> will randomly be selected.
    /// </summary>
    public bool RandomizeEndPointResolving { get; set; } = true;

    /// <summary>
    /// Virtual host for the RabbitMQ connection.
    /// </summary>
    public string VHost { get; set; } = string.Empty;

    /// <summary>
    /// Username for authenticating the RabbitMQ connection.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Password for authenticating the RabbitMQ connection.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Service name associated with the broker connection.
    /// </summary>
    /// <remarks>Used to set <see cref="CarrotHeader.ServiceName" /></remarks>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Unique ID identifying this service instance
    /// </summary>
    /// <value>Defaults to <see cref="Guid.NewGuid()" /></value>
    public Guid ServiceInstanceId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name for the RabbitMQ connection.
    /// </summary>
    /// <remarks>Defaults to "<see cref="ServiceName" /> <see cref="ServiceInstanceId" />"</remarks>
    public string? DisplayConnectionName { get; set; }

    /// <summary>
    /// Dictionary of properties that are sent to the broker (stored on the BrokerConnection)
    /// </summary>
    public IDictionary<string, object?> ClientProperties { get; set; } = new Dictionary<string, object?>();

    /// <summary>
    /// Initial connection timeout.
    /// </summary>
    public TimeSpan InitialConnectionTimeout { get; set; } = Timeout.InfiniteTimeSpan;

    /// <summary>
    /// Concurrency level for consumer dispatch.<br />
    /// Defaults to <c>4</c>.
    /// </summary>
    /// <remarks>
    /// 0 means ConsumerDispatchConcurrency is not set on the ConnectionFactory, defaults to 1<br />
    /// > 1 enables concurrent processing <br /><br />
    /// The work will be offloaded to the worker thread pool so it is important to choose the value for the concurrency wisely
    /// to avoid thread pool overloading.
    /// </remarks>
    public ushort ConsumerDispatchConcurrency { get; set; } = 4;

    /// <summary>
    /// Configure options on <see cref="ConnectionFactory" />
    /// </summary>
    public Action<ConnectionFactory>? ConfigureConnectionFactory { get; set; }

    /// <summary>
    /// Configuration for PublisherConfirm
    /// </summary>
    public PublisherConfirmOptions PublisherConfirm { get; set; } = new();
}