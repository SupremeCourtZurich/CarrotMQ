using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Serialization;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <inheritdoc cref="IPublisherChannel" />
internal class PublisherChannel : CarrotChannel, IPublisherChannel
{
    protected PublisherChannel(
        IConnection connection,
        TimeSpan networkRecoveryInterval,
        IProtocolSerializer protocolSerializer,
        IBasicPropertiesMapper basicPropertiesMapper,
        ILoggerFactory loggerFactory)
        : base(connection, networkRecoveryInterval, protocolSerializer, basicPropertiesMapper, loggerFactory)
    {

    }



    /// <inheritdoc />
    public virtual async Task PublishAsync(CarrotMessage message, CancellationToken token)
    {
        using var scope = await ChannelLock.LockAsync().ConfigureAwait(false);
        var basicProperties = CreateBasicProperties(message.Header);

        string payload = ProtocolSerializer.Serialize(message);

        await Channel!.BasicPublishAsync(
                message.Header.Exchange,
                message.Header.RoutingKey,
                false,
                basicProperties,
                Encoding.UTF8.GetBytes(payload),
                token)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="IPublisherChannel" /> interface.
    /// </summary>
    /// <param name="connection">The broker connection associated with the channel.</param>
    /// <param name="networkRecoveryInterval"></param>
    /// <param name="protocolSerializer">The serializer for <see cref="CarrotMessage" />.</param>
    /// <param name="basicPropertiesMapper">Mapper for the messages basic properties.</param>
    /// <param name="loggerFactory">The logger factory used to create loggers.</param>
    /// <returns>A new instance of the <see cref="IPublisherChannel" />.</returns>
    public new static async Task<IPublisherChannel> CreateAsync(
        IConnection connection,
        TimeSpan networkRecoveryInterval,
        IProtocolSerializer protocolSerializer,
        IBasicPropertiesMapper basicPropertiesMapper,
        ILoggerFactory loggerFactory)
    {
        var channel = new PublisherChannel(connection, networkRecoveryInterval, protocolSerializer, basicPropertiesMapper, loggerFactory);
        await channel.CreateChannelAsync().ConfigureAwait(false);

        return channel;
    }

    protected virtual BasicProperties CreateBasicProperties(CarrotHeader header)
    {
        return BasicPropertiesMapper.CreateBasicProperties(header);
    }
}