using System;
using System.Collections.Generic;
using System.Linq;
using CarrotMQ.RabbitMQ.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// Default EndPoint resolver. Transforms <see cref="BrokerConnectionOptions.BrokerEndPoints" /> to
/// <see cref="AmqpTcpEndpoint" />s
/// </summary>
public sealed class DefaultCarrotEndPointResolver : IEndpointResolver
{
    private readonly IEnumerable<AmqpTcpEndpoint> _amqpEndpoint;
    private readonly bool _randomizeEndPointResolving;

    private readonly Random _rnd = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCarrotEndPointResolver" /> class.
    /// </summary>
    /// <param name="brokerConnectionOptions">The options for configuring broker connection.</param>
    public DefaultCarrotEndPointResolver(IOptions<BrokerConnectionOptions> brokerConnectionOptions)
    {
        _randomizeEndPointResolving = brokerConnectionOptions.Value.RandomizeEndPointResolving;

        _amqpEndpoint = brokerConnectionOptions.Value.BrokerEndPoints.Select(uri => new AmqpTcpEndpoint(uri));
    }

    /// <summary>
    /// For testing only
    /// </summary>
    internal DefaultCarrotEndPointResolver(IOptions<BrokerConnectionOptions> brokerConnectionOptions, Random rnd) : this(brokerConnectionOptions)
    {
        _rnd = rnd;
    }

    /// <summary>
    /// Return all AmqpTcpEndpoints in the order they should be tried.
    /// </summary>
    /// <returns>
    /// if <see cref="BrokerConnectionOptions.RandomizeEndPointResolving" /> is true returns randomly ordered
    /// endpoints, otherwise they are ordered as specified in <see cref="BrokerConnectionOptions.BrokerEndPoints" />
    /// </returns>
    public IEnumerable<AmqpTcpEndpoint> All()
    {
        return _randomizeEndPointResolving ? _amqpEndpoint.OrderBy(_ => _rnd.Next()) : _amqpEndpoint;
    }
}