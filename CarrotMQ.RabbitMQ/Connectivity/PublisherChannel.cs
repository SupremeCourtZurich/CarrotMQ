using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Protocol;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <inheritdoc cref="IPublisherChannel" />
internal class PublisherChannel : CarrotChannel, IPublisherChannel
{
    protected PublisherChannel(
        IConnection connection,
        TimeSpan networkRecoveryInterval,
        ILoggerFactory loggerFactory)
        : base(connection, networkRecoveryInterval, loggerFactory)
    {
    }

    /// <inheritdoc />
    public virtual async Task PublishAsync(string payload, CarrotHeader messageHeader, CancellationToken token)
    {
        using var scope = await ChannelLock.LockAsync().ConfigureAwait(false);
        var basicProperties = CreateBasicProperties(messageHeader);

        await Channel!.BasicPublishAsync(
                messageHeader.Exchange,
                messageHeader.RoutingKey,
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
    /// <param name="loggerFactory">The logger factory used to create loggers.</param>
    /// <returns>A new instance of the <see cref="IPublisherChannel" />.</returns>
    public new static async Task<IPublisherChannel> CreateAsync(
        IConnection connection,
        TimeSpan networkRecoveryInterval,
        ILoggerFactory loggerFactory)
    {
        var channel = new PublisherChannel(connection, networkRecoveryInterval, loggerFactory);
        await channel.CreateChannelAsync().ConfigureAwait(false);

        return channel;
    }

    protected virtual BasicProperties CreateBasicProperties(CarrotHeader header)
    {
        var unixTime = DateTimeOffset.Now.ToUnixTimeSeconds();

        var basicProperties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = header.MessageProperties.Persistent,
            Priority = header.MessageProperties.Priority,
            CorrelationId = header.CorrelationId?.ToString(),
            Expiration = header.MessageProperties.Ttl >= 0 ? header.MessageProperties.Ttl.ToString() : null,
            MessageId = header.MessageId.ToString(),
            Timestamp = new AmqpTimestamp(unixTime),
            Type = nameof(CarrotMessage),
            AppId = header.ServiceInstanceId.ToString()
        };

        return basicProperties;
    }
}