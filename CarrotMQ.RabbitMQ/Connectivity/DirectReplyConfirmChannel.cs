using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Serialization;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// Represents a channel for direct reply communication pattern.
/// <see href="https://www.rabbitmq.com/direct-reply-to.html">Direct Reply-to</see>
/// </summary>
internal sealed class DirectReplyConfirmChannel : PublisherConfirmChannel, IDirectReplyChannel
{
    private const string QueueName = "amq.rabbitmq.reply-to";
    private const bool AutoAck = true;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<CarrotMessage>> _replyMapper = new();

    private DirectReplyConfirmChannel(
        IConnection connection,
        TimeSpan networkRecoveryInterval,
        PublisherConfirmOptions publisherConfirmOptions,
        IProtocolSerializer protocolSerializer,
        ILoggerFactory loggerFactory)
        : base(connection, networkRecoveryInterval, publisherConfirmOptions, protocolSerializer, loggerFactory)
    {
    }

    /// <summary>
    /// Publishes a message with a direct reply pattern asynchronously.
    /// </summary>
    public async Task<CarrotMessage> PublishWithReplyAsync(
        CarrotMessage message,
        CancellationToken token)
    {
        var correlationId = (Guid)message.Header.CorrelationId!;

        var tcs = new TaskCompletionSource<CarrotMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _replyMapper.TryAdd(correlationId, tcs);

        token.Register(
            () =>
            {
                Logger.LogDebug("Cancel publish with direct reply; CorrelationId={CorrelationId}", correlationId);
                tcs.TrySetCanceled();
                _replyMapper.TryRemove(correlationId, out _);
            },
            false);

        Logger.LogDebug("Publish with direct reply; CorrelationId={CorrelationId}", correlationId);
        await PublishAsync(message, token).ConfigureAwait(false);

        var result = await tcs.Task.ConfigureAwait(false);

        return result;
    }

    protected override BasicProperties CreateBasicProperties(CarrotHeader header)
    {
        var basicProperties = base.CreateBasicProperties(header);
        basicProperties.ReplyTo = QueueName;

        return basicProperties;
    }

    /// <summary>
    /// Creates a new instance of <see cref="IDirectReplyChannel" />.
    /// </summary>
    /// <param name="connection">The broker connection.</param>
    /// <param name="networkRecoveryInterval"></param>
    /// <param name="publisherConfirmOptions">The options for publisher confirms.</param>
    /// <param name="protocolSerializer">The serializer for <see cref="CarrotMessage" />.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>A new instance of <see cref="IDirectReplyChannel" />.</returns>
    public static async Task<IDirectReplyChannel> CreateAsync(
        IConnection connection,
        TimeSpan networkRecoveryInterval,
        PublisherConfirmOptions publisherConfirmOptions,
        IProtocolSerializer protocolSerializer,
        ILoggerFactory loggerFactory)
    {
        var channel = new DirectReplyConfirmChannel(
            connection,
            networkRecoveryInterval,
            publisherConfirmOptions,
            protocolSerializer,
            loggerFactory);
        await channel.CreateChannelAsync().ConfigureAwait(false);
        channel.StartRepublishingTimer();

        return channel;
    }

    protected override async Task CreateChannelAsync()
    {
        await base.CreateChannelAsync().ConfigureAwait(false);

        var consumer = new AsyncEventingBasicConsumer(Channel!);
        consumer.ReceivedAsync += ConsumeAsync;
        await Channel!.BasicConsumeAsync(QueueName, AutoAck, consumer).ConfigureAwait(false);
    }

    private Task ConsumeAsync(object sender, BasicDeliverEventArgs eventArgs)
    {
        var correlationId = new Guid(eventArgs.BasicProperties.CorrelationId ?? string.Empty);
        Logger.LogDebug("Direct reply received; CorrelationId={CorrelationId}", correlationId);

        if (!_replyMapper.TryRemove(correlationId, out var tcs)) return Task.CompletedTask;

        var payload = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
        var message = ProtocolSerializer.Deserialize(payload, eventArgs.BasicProperties);

        tcs.TrySetResult(message);

        return Task.CompletedTask;
    }
}