using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly uint _maxRetries;
    private readonly ConcurrentDictionary<ulong, PublisherConfirmMessage> _outstandingConfirms = new();
    private readonly SemaphoreSlim _outstandingConfirmsCount;
    private readonly ConcurrentQueue<PublisherConfirmMessage> _publishedOnClosedChannel = new();
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

    /// <summary>
    /// The number of messages waiting for the closed channel to be reopened.
    /// </summary>
    internal int PublishedOnClosedChannelCount => _publishedOnClosedChannel.Count;

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
    /// <exception cref="PublishFailedException">
    /// Thrown when the <paramref name="token" /> is canceled before the broker confirmed the message; the
    /// <see cref="OperationCanceledException" /> is the inner exception.
    /// </exception>
    /// <exception cref="Exception">Thrown when an error occurs during the publish operation.</exception>
    public override async Task PublishAsync(CarrotMessage message, CancellationToken token)
    {
        using (var slotWait = CancellationTokenSource.CreateLinkedTokenSource(token, _disposeCts.Token))
        {
            try
            {
                await _outstandingConfirmsCount.WaitAsync(slotWait.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (token.IsCancellationRequested)
            {
                throw PublishFailure.Cancelled(message.Header, ReasonAtCancellation(PublishFailureReason.Unknown), ex);
            }
            catch (OperationCanceledException)
            {
                throw new ObjectDisposedException(nameof(PublisherConfirmChannel));
            }
        }

        try
        {
            await PublishAndWaitForConfirmAsync(message, token).ConfigureAwait(false);
        }
        finally
        {
            _outstandingConfirmsCount.Release();
        }
    }

    private PublishFailureReason ReasonAtCancellation(PublishFailureReason lastFailure)
    {
        return PublishFailure.ReasonAtCancellation(Connection, IsOpen, LastShutdownReason, lastFailure);
    }

    private async Task PublishAndWaitForConfirmAsync(CarrotMessage message, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var basicProperties = CreateBasicProperties(message.Header);
        string payload = ProtocolSerializer.Serialize(message, basicProperties);
        var confirmMessage = new PublisherConfirmMessage(payload, message.Header, basicProperties, tcs, token);
#if NET
        var reg = token.Register(() => { confirmMessage.CompletionSource.TrySetCanceled(); });
        await using var unused = reg.ConfigureAwait(false);
        var disposeReg = _disposeCts.Token.Register(() => tcs.TrySetException(new ObjectDisposedException(nameof(PublisherConfirmChannel))));
        await using var unusedDispose = disposeReg.ConfigureAwait(false);
#else
#pragma warning disable IDE0063
        // IAsyncDisposable does not exist in .NET Standard --> call 'using' without 'await' --> calling .Dispose() instead of .DisposeAsync()
        // ReSharper disable once ConvertToUsingDeclaration
        using var unused = token.Register(() => { confirmMessage.CompletionSource.TrySetCanceled(); });
        using var unusedDispose = _disposeCts.Token.Register(() => tcs.TrySetException(new ObjectDisposedException(nameof(PublisherConfirmChannel))));
#pragma warning restore IDE0063
#endif
        try
        {
            using (await ChannelLock.LockAsync(token).ConfigureAwait(false))
            {
                await PublishInternallyAsync(confirmMessage).ConfigureAwait(false);
            }

            await confirmMessage.CompletionSource.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (token.IsCancellationRequested)
        {
            throw PublishFailure.Cancelled(message.Header, ReasonAtCancellation(confirmMessage.LastFailureReason), ex);
        }
    }

    private async Task PublishInternallyAsync(PublisherConfirmMessage confirmMessage)
    {
        if (confirmMessage.CancellationToken.IsCancellationRequested) return;

        // A channel known to be closed is never handed a publish: the client might send it after its close-ok.
        if (!IsOpen)
        {
            if (LastShutdownReason != null) PublishFailure.RecordClose(confirmMessage, LastShutdownReason);
            _publishedOnClosedChannel.Enqueue(confirmMessage);

            return;
        }

        var seqNo = await Channel!.GetNextPublishSequenceNumberAsync(confirmMessage.CancellationToken).ConfigureAwait(false);

        // The client restarts sequence numbers on a channel it recovered by itself.
        if (seqNo <= _lastPublishedSeqNo) await FlagAllMessagesForRepublishAsync().ConfigureAwait(false);

        confirmMessage.PublishedAt = _dateTimeProvider.Now;
        confirmMessage.SeqNo = seqNo;
        _lastPublishedSeqNo = seqNo;
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
        catch (Exception exception)
        {
            // A failed publish does not advance the sequence number, so the next one reuses it: keep the message by order.
            if (_outstandingConfirms.TryGetValue(confirmMessage.SeqNo, out var tracked) && ReferenceEquals(tracked, confirmMessage))
            {
                _outstandingConfirms.TryRemove(confirmMessage.SeqNo, out _);
            }

            if (_lastPublishedSeqNo == confirmMessage.SeqNo) _lastPublishedSeqNo--;
            if (exception is not AlreadyClosedException closed) throw;

            Logger.LogWarning(closed, "Channel was already closed when publishing. Publish is enqueued for retry.");
            if (closed.ShutdownReason != null) PublishFailure.RecordClose(confirmMessage, closed.ShutdownReason);
            _publishedOnClosedChannel.Enqueue(confirmMessage);
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

        // Fails every publish still waiting for a slot, its confirm or the channel to reopen.
        // The slot semaphore stays undisposed: publishes completing after this still release their slot.
        _disposeCts.Cancel();

        await base.DisposeAsync().ConfigureAwait(false);
        _disposeCts.Dispose();
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
                    message.CompletionSource.TrySetResult(true);
                }
            }
        }
        else
        {
            if (_outstandingConfirms.TryRemove(e.DeliveryTag, out var message))
            {
                message.CompletionSource.TrySetResult(true);
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
                    PublishFailure.RecordNoConfirm(message);
                    _republishQueue.Enqueue(message);
                }
            }
        }
        else
        {
            if (_outstandingConfirms.TryRemove(e.DeliveryTag, out var message))
            {
                PublishFailure.RecordNoConfirm(message);
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
    /// Move all messages from <see cref="_outstandingConfirms" /> and <see cref="_publishedOnClosedChannel" /> to
    /// <see cref="_republishQueue" />.
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
            if (_outstandingConfirms.TryRemove(oldTailSeqNo, out var oldestOutstandingConfirm)) Republish(oldestOutstandingConfirm);
        }

        while (_publishedOnClosedChannel.TryDequeue(out var unsent))
        {
            Republish(unsent);
        }

        void Republish(PublisherConfirmMessage message)
        {
            if (LastShutdownReason != null) PublishFailure.RecordClose(message, LastShutdownReason);
            _republishQueue.Enqueue(message);
        }
    }

    private async Task RepublishOnTimedEventAsync()
    {
        using var scope = await ChannelLock.LockAsync().ConfigureAwait(false);

        // Messages whose publisher has given up are dropped while the channel stays closed.
        for (var n = _publishedOnClosedChannel.Count; n > 0 && _publishedOnClosedChannel.TryDequeue(out var queued); n--)
        {
            if (!queued.CancellationToken.IsCancellationRequested) _publishedOnClosedChannel.Enqueue(queued);
        }

        // A channel that recovered by itself is not reopened here, so its unsent messages are not handed over.
        if (Channel is { IsOpen: true })
        {
            while (_publishedOnClosedChannel.TryDequeue(out var unsent))
            {
                _republishQueue.Enqueue(unsent);
            }
        }

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
            confirmMessage.CompletionSource.TrySetException(PublishFailure.RetryLimitExceeded(confirmMessage, _maxRetries));

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
            confirmMessage.CompletionSource.TrySetException(e);
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
        var timedOut = new List<PublisherConfirmMessage>();
        using (await _tailSeqNoLock.LockAsync().ConfigureAwait(false))
        {
            var republishThresholdTime = _dateTimeProvider.Now.AddMilliseconds(-_retryIntervalInMs);
            while ((_outstandingConfirms.TryGetValue(_tailSeqNo, out var oldestOutstandingConfirm)
                    && oldestOutstandingConfirm.PublishedAt < republishThresholdTime)
                || (oldestOutstandingConfirm == null && _tailSeqNo <= _lastPublishedSeqNo))
            {
                if (oldestOutstandingConfirm != null)
                {
                    _outstandingConfirms.TryRemove(_tailSeqNo, out _);
                    PublishFailure.RecordNoConfirm(oldestOutstandingConfirm);
                    timedOut.Add(oldestOutstandingConfirm);
                }

                _tailSeqNo++;
            }
        }

        // Republished outside the lock: a publish may reset the bookkeeping, which takes it.
        foreach (var message in timedOut)
        {
            await PublishWithRetryCheckAsync(message).ConfigureAwait(false);
        }
    }
}