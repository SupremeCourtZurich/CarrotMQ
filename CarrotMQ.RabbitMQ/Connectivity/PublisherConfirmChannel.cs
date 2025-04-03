using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Common;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Serialization;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// Represents a publisher confirm channel that provides additional functionality for handling message confirmations.
/// </summary>
internal class PublisherConfirmChannel : PublisherChannel
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly uint _maxRetries;
    private readonly ConcurrentDictionary<ulong, PublisherConfirmMessage> _outstandingConfirms = new();
    private readonly SemaphoreSlim _outstandingConfirmsCount;
    private readonly ConcurrentQueue<PublisherConfirmMessage> _republishQueue = new();
    private readonly uint _retryIntervalInMs;
    private readonly AsyncLock _tailSeqNoLock = new();
    private readonly IIntervalTimer _timer;

    /// <summary>
    /// Sequence number of the latest published message
    /// </summary>
    private ulong _lastPublishedSeqNo;

    /// <summary>
    /// Sequence number of the oldest unconfirmed message or a future message to confirm if there are no current messages to
    /// confirm
    /// </summary>
    private ulong _tailSeqNo = 1;

    protected PublisherConfirmChannel(
        IConnection connection,
        TimeSpan networkRecoveryInterval,
        PublisherConfirmOptions options,
        IProtocolSerializer protocolSerializer,
        ILoggerFactory loggerFactory,
        IIntervalTimer? intervalTimer = null,
        IDateTimeProvider? dateTimeProvider = null)
        : base(connection, networkRecoveryInterval, protocolSerializer, loggerFactory)
    {
        _dateTimeProvider = dateTimeProvider ?? new DateTimeProvider();
        _timer = intervalTimer ?? new IntervalTimer(options.RepublishEvaluationIntervalInMs);
        _timer.ElapsedAsync += async (_, _) => await RepublishOnTimedEventAsync().ConfigureAwait(false);
        _outstandingConfirmsCount = new SemaphoreSlim(options.MaxConcurrentConfirms);
        _retryIntervalInMs = options.RetryIntervalInMs;
        _maxRetries = options.RetryLimit;
    }

    protected override CreateChannelOptions CreateChannelOptions()
    {
        return new CreateChannelOptions(true, false, consumerDispatchConcurrency: null);
    }

    /// <summary>
    /// Creates a new instance of the <see cref="IPublisherChannel" /> interface with publisher confirms.
    /// </summary>
    /// <param name="connection">The broker connection associated with the channel.</param>
    /// <param name="networkRecoveryInterval"></param>
    /// <param name="options">The options for publisher confirms.</param>
    /// <param name="protocolSerializer">The serializer for <see cref="CarrotMessage" />.</param>
    /// <param name="loggerFactory">The logger factory used to create loggers.</param>
    /// <param name="intervalTimer">The interval timer for republishing.</param>
    /// <param name="dateTimeProvider">The date and time provider for time-related operations.</param>
    /// <returns>A new instance of the <see cref="IPublisherChannel" /> interface with publisher confirms.</returns>
    public static async Task<IPublisherChannel> CreateAsync(
        IConnection connection,
        TimeSpan networkRecoveryInterval,
        PublisherConfirmOptions options,
        IProtocolSerializer protocolSerializer,
        ILoggerFactory loggerFactory,
        IIntervalTimer? intervalTimer = null,
        IDateTimeProvider? dateTimeProvider = null)
    {
        var channel = new PublisherConfirmChannel(
            connection,
            networkRecoveryInterval,
            options,
            protocolSerializer,
            loggerFactory,
            intervalTimer,
            dateTimeProvider);

        await channel.CreateChannelAsync().ConfigureAwait(false);
        channel.StartRepublishingTimer();

        return channel;
    }

    /// <summary>
    /// Publishes a message asynchronously with publisher confirms.
    /// </summary>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the <paramref name="token" /> is canceled before the publish operation completes.
    /// </exception>
    /// <exception cref="Exception">Thrown when an error occurs during the publish operation.</exception>
    public override async Task PublishAsync(CarrotMessage message, CancellationToken token)
    {
        await _outstandingConfirmsCount.WaitAsync(token).ConfigureAwait(false);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var basicProperties = CreateBasicProperties(message.Header);
        string payload = ProtocolSerializer.Serialize(message, basicProperties);
        var confirmMessage = new PublisherConfirmMessage(payload, message.Header, basicProperties, tcs, token);
#if NET
        var reg = token.Register(() => { confirmMessage.CompletionSource.TrySetCanceled(); });
        await using var unused = reg.ConfigureAwait(false);
#else
#pragma warning disable IDE0063
        // IAsyncDisposable does not exist in .NET Standard --> call 'using' without 'await' --> calling .Dispose() instead of .DisposeAsync()
        // ReSharper disable once ConvertToUsingDeclaration
        using var unused = token.Register(() => { confirmMessage.CompletionSource.TrySetCanceled(); });
#pragma warning restore IDE0063
#endif
        try
        {
            using (await ChannelLock.LockAsync().ConfigureAwait(false))
            {
                await PublishInternallyAsync(confirmMessage).ConfigureAwait(false);
            }

            await confirmMessage.CompletionSource.Task.ConfigureAwait(false);
        }
        finally
        {
            _outstandingConfirmsCount.Release();
        }
    }

    private async Task PublishInternallyAsync(PublisherConfirmMessage confirmMessage)
    {
        if (confirmMessage.CancellationToken.IsCancellationRequested) return;

        confirmMessage.PublishedAt = _dateTimeProvider.Now;
        confirmMessage.SeqNo = await Channel!.GetNextPublishSequenceNumberAsync(confirmMessage.CancellationToken).ConfigureAwait(false);
        _lastPublishedSeqNo = confirmMessage.SeqNo;
        _outstandingConfirms.TryAdd(confirmMessage.SeqNo, confirmMessage);

        var header = confirmMessage.MessageHeader;

        try
        {
            await Channel!.BasicPublishAsync(
                    header.Exchange,
                    header.RoutingKey,
                    false,
                    confirmMessage.BasicProperties,
                    confirmMessage.Payload,
                    confirmMessage.CancellationToken)
                .ConfigureAwait(false);
            Logger.LogTrace("Published message: channel={ChannelNumber}, seq={SeqNo}", Channel?.ChannelNumber, confirmMessage.SeqNo);
        }
        catch (AlreadyClosedException exception)
        {
            Logger.LogWarning(exception, "Channel was already closed when publishing. Publish is enqueued for retry.");
        }
    }

    protected void StartRepublishingTimer()
    {
        _timer.Start();
    }

    protected override async Task CreateChannelAsync()
    {
        await base.CreateChannelAsync().ConfigureAwait(false);

        Channel!.BasicAcksAsync += BasicAcksAsync;
        Channel!.BasicNacksAsync += BasicNacksAsync;
        Channel!.BasicReturnAsync += BasicReturnAsync;

        await FlagAllMessagesForRepublishAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        _timer.Dispose();
        _outstandingConfirmsCount.Dispose();
        await base.DisposeAsync().ConfigureAwait(false);
    }

    private async Task BasicAcksAsync(object sender, BasicAckEventArgs e)
    {
        Logger.LogTrace(
            "positive acknowledgment received; channel={ChannelNumber}, seq={SeqNo}, multiple={Multiple}",
            Channel?.ChannelNumber,
            e.DeliveryTag,
            e.Multiple);
        using var scope = await _tailSeqNoLock.LockAsync().ConfigureAwait(false);
        if (e.Multiple)
        {
            for (; _tailSeqNo <= e.DeliveryTag; _tailSeqNo++)
            {
                if (_outstandingConfirms.TryRemove(_tailSeqNo, out var message))
                {
                    message.CompletionSource.SetResult(true);
                }
            }
        }
        else
        {
            if (_outstandingConfirms.TryRemove(e.DeliveryTag, out var message))
            {
                message.CompletionSource.SetResult(true);
            }

            if (e.DeliveryTag != _tailSeqNo) return;

            _tailSeqNo++;
        }
    }

    private async Task BasicNacksAsync(object sender, BasicNackEventArgs e)
    {
        Logger.LogTrace(
            "negative acknowledgment received; channel={ChannelNumber}, seq={SeqNo}, multiple={Multiple}",
            Channel?.ChannelNumber,
            e.DeliveryTag,
            e.Multiple);

        using var scope = await _tailSeqNoLock.LockAsync().ConfigureAwait(false);
        if (e.Multiple)
        {
            for (; _tailSeqNo <= e.DeliveryTag; _tailSeqNo++)
            {
                if (_outstandingConfirms.TryRemove(_tailSeqNo, out var message))
                {
                    _republishQueue.Enqueue(message);
                }
            }
        }
        else
        {
            if (_outstandingConfirms.TryRemove(e.DeliveryTag, out var message))
            {
                _republishQueue.Enqueue(message);
            }

            if (e.DeliveryTag != _tailSeqNo) return;

            _tailSeqNo++;
        }
    }

    private Task BasicReturnAsync(object sender, BasicReturnEventArgs e)
    {
        Logger.LogTrace(
            "basic.return received; channel={ChannelNumber}, exchange={Exchange}, routingKey={RoutingKey}, replyCode={ReplyCode}, replyText={ReplyText}",
            Channel?.ChannelNumber,
            e.Exchange,
            e.RoutingKey,
            e.ReplyCode,
            e.ReplyText);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Move all messages from <see cref="_outstandingConfirms" /> to <see cref="_republishQueue" />
    /// This happens after a channel shutdown --> all messages have to be republished with a new sequence number
    /// </summary>
    private async Task FlagAllMessagesForRepublishAsync()
    {
        using var scope = await _tailSeqNoLock.LockAsync().ConfigureAwait(false);
        var oldTailSeqNo = _tailSeqNo;
        var oldHeadSeqNo = _lastPublishedSeqNo;
        _tailSeqNo = 1;
        _lastPublishedSeqNo = 0;
        for (; oldTailSeqNo <= oldHeadSeqNo; oldTailSeqNo++)
        {
            if (_outstandingConfirms.TryRemove(oldTailSeqNo, out var oldestOutstandingConfirm))
            {
                _republishQueue.Enqueue(oldestOutstandingConfirm);
            }
        }
    }

    private async Task RepublishOnTimedEventAsync()
    {
        using var scope = await ChannelLock.LockAsync().ConfigureAwait(false);

        while (_republishQueue.TryDequeue(out var publisherConfirmMessage))
        {
            await PublishWithRetryCheckAsync(publisherConfirmMessage).ConfigureAwait(false);
        }

        if (_tailSeqNo <= _lastPublishedSeqNo)
        {
            await RepublishTimedOutMessagesAsync().ConfigureAwait(false);
        }
    }

    private async Task PublishWithRetryCheckAsync(PublisherConfirmMessage confirmMessage)
    {
        confirmMessage.RepublishCount++;
        if (confirmMessage.RepublishCount > _maxRetries)
        {
            confirmMessage.CompletionSource.SetException(new RetryLimitExceededException());

            return;
        }

        try
        {
            await PublishInternallyAsync(confirmMessage).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // already covered through token.Register(...)
        }
        catch (Exception e)
        {
            Logger.LogTrace(e, "Caught exception while publishing");
            confirmMessage.CompletionSource.SetException(e);
        }
    }

    /// <summary>
    /// Repeatedly evaluates message at current tail position:
    /// - if it exists, and it is timed out -> republish and move next
    /// - if it does not exist (already acked or nacked) and there are more message -> move next
    /// - else return (tail points to the oldest message awaiting confirm)
    /// </summary>
    private async Task RepublishTimedOutMessagesAsync()
    {
        using var scope = await _tailSeqNoLock.LockAsync().ConfigureAwait(false);
        var republishThresholdTime = _dateTimeProvider.Now.AddMilliseconds(-_retryIntervalInMs);
        while ((_outstandingConfirms.TryGetValue(_tailSeqNo, out var oldestOutstandingConfirm)
                && oldestOutstandingConfirm.PublishedAt < republishThresholdTime)
            || (oldestOutstandingConfirm == null && _tailSeqNo <= _lastPublishedSeqNo))
        {
            if (oldestOutstandingConfirm != null)
            {
                _outstandingConfirms.TryRemove(_tailSeqNo, out _);
                await PublishWithRetryCheckAsync(oldestOutstandingConfirm).ConfigureAwait(false);
            }

            _tailSeqNo++;
        }
    }
}