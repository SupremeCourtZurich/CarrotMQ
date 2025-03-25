using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Serialization;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// Represents a channel for direct reply communication pattern.
/// <see href="https://www.rabbitmq.com/direct-reply-to.html">Direct Reply-to</see>
/// </summary>
internal sealed class DirectReplyChannel : PublisherChannel, IDirectReplyChannel
{
    private const string QueueName = "amq.rabbitmq.reply-to";
    private const bool AutoAck = true;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<string>> _replyMapper = new();

    private DirectReplyChannel(
        IConnection connection,
        TimeSpan networkRecoveryInterval, 
        IBasicPropertiesMapper basicPropertiesMapper,
        ILoggerFactory loggerFactory)
        : base(connection, networkRecoveryInterval, basicPropertiesMapper, loggerFactory)
    {
    }

    /// <inheritdoc />
    public async Task<string> PublishWithReplyAsync(
        string messagePayload,
        CarrotHeader carrotHeader,
        CancellationToken token)
    {
        var correlationId = (Guid)carrotHeader.CorrelationId!;

        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
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
        await PublishAsync(messagePayload, carrotHeader, token).ConfigureAwait(false);

        var result = await tcs.Task.ConfigureAwait(false);

        return result;
    }

    /// <inheritdoc />
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
    /// <param name="basicPropertiesMapper">Mapper for the messages basic properties.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>A new instance of <see cref="IDirectReplyChannel" />.</returns>
    public new static async Task<IDirectReplyChannel> CreateAsync(
        IConnection connection,
        TimeSpan networkRecoveryInterval,
        IBasicPropertiesMapper basicPropertiesMapper,
        ILoggerFactory loggerFactory)
    {
        var channel = new DirectReplyChannel(connection, networkRecoveryInterval, basicPropertiesMapper, loggerFactory);
        await channel.CreateChannelAsync().ConfigureAwait(false);

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
        tcs.TrySetResult(payload);

        return Task.CompletedTask;
    }
}