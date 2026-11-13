using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Protocol;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// Republishes the messages of a channel the broker closed over a refused publish, one at a time on channels of its
/// own: a recovery channel for the messages, and a quarantine channel for those it identifies as refused.
/// Each channel is opened on demand and closed once idle.
/// </summary>
internal sealed class PublishRecoveryChannel : IAsyncDisposable
{
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly SerialPublisher _quarantine;
    private readonly SerialPublisher _recovery;

    public PublishRecoveryChannel(
        IConnection connection,
        CreateChannelOptions channelOptions,
        uint maxRetries,
        TimeSpan retryInterval,
        TimeSpan networkRecoveryInterval,
        TimeSpan idleTimeout,
        Func<IChannel, Task> onChannelCreated,
        Func<CarrotHeader, Task> waitForReply,
        ILogger logger)
    {
        var settings = new Settings(
            connection,
            channelOptions,
            maxRetries,
            retryInterval,
            networkRecoveryInterval,
            idleTimeout,
            onChannelCreated,
            waitForReply,
            logger,
            _disposeCts.Token);

        _quarantine = new SerialPublisher("quarantine", settings, null);
        _recovery = new SerialPublisher("recovery", settings, _quarantine);
    }

    /// <summary>
    /// <c>true</c> if the broker closed the channel because of a <c>basic.publish</c> (AMQP class 60, method 40).
    /// </summary>
    public static bool IsPublishRefusal(ShutdownEventArgs? reason)
    {
        return reason is { Initiator: ShutdownInitiator.Peer, ClassId: 60, MethodId: 40 };
    }

    public void Enqueue(PublisherConfirmMessage message)
    {
        _recovery.Enqueue(message);
    }

    public async ValueTask DisposeAsync()
    {
        _disposeCts.Cancel();
        await _recovery.DisposeAsync().ConfigureAwait(false);
        await _quarantine.DisposeAsync().ConfigureAwait(false);
    }

    private sealed class Settings
    {
        public Settings(
            IConnection connection,
            CreateChannelOptions channelOptions,
            uint maxRetries,
            TimeSpan retryInterval,
            TimeSpan networkRecoveryInterval,
            TimeSpan idleTimeout,
            Func<IChannel, Task> onChannelCreated,
            Func<CarrotHeader, Task> waitForReply,
            ILogger logger,
            CancellationToken disposeToken)
        {
            Connection = connection;
            ChannelOptions = channelOptions;
            MaxRetries = maxRetries;
            RetryInterval = retryInterval;
            NetworkRecoveryInterval = networkRecoveryInterval;
            IdleTimeout = idleTimeout;
            OnChannelCreated = onChannelCreated;
            WaitForReply = waitForReply;
            Logger = logger;
            DisposeToken = disposeToken;
        }

        public IConnection Connection { get; }

        public CreateChannelOptions ChannelOptions { get; }

        public uint MaxRetries { get; }

        public TimeSpan RetryInterval { get; }

        public TimeSpan NetworkRecoveryInterval { get; }

        public TimeSpan IdleTimeout { get; }

        public Func<IChannel, Task> OnChannelCreated { get; }

        public Func<CarrotHeader, Task> WaitForReply { get; }

        public ILogger Logger { get; }

        public CancellationToken DisposeToken { get; }
    }

    /// <summary>
    /// Publishes queued messages one at a time on one channel, awaiting each confirm and, for a request, its reply.
    /// </summary>
    /// <remarks>
    /// <see cref="Enqueue" /> may be called from any thread; at most one loop runs at a time and only it opens, uses and
    /// closes the channel.
    /// </remarks>
    private sealed class SerialPublisher
    {
        private readonly string _name;
        private readonly ConcurrentQueue<PublisherConfirmMessage> _queue = new();
        private readonly SerialPublisher? _refusedTarget;
        private readonly Settings _settings;
        private readonly SemaphoreSlim _signal = new(0);
        private Attempt? _attempt;
        private IChannel? _current;
        private Task _loopTask = Task.CompletedTask;
        private int _pendingRequeues;
        private int _running;

        // Refused messages go to refusedTarget; without one they are retried here.
        public SerialPublisher(string name, Settings settings, SerialPublisher? refusedTarget)
        {
            _name = name;
            _settings = settings;
            _refusedTarget = refusedTarget;
        }

        public void Enqueue(PublisherConfirmMessage message)
        {
            if (_settings.DisposeToken.IsCancellationRequested) return;

            _queue.Enqueue(message);
            _signal.Release();

            if (Interlocked.CompareExchange(ref _running, 1, 0) == 0)
            {
                Volatile.Write(ref _loopTask, Task.Run(RunAsync));
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await Volatile.Read(ref _loopTask).ConfigureAwait(false);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _settings.Logger.LogWarning(e, "Publish {Name} loop failed", _name);
            }
            catch (OperationCanceledException)
            {
                // disposing
            }

            if (_current != null) await DisposeChannelAsync(_current).ConfigureAwait(false);
        }

        private void EnqueueAfter(PublisherConfirmMessage message, TimeSpan delay)
        {
            Interlocked.Increment(ref _pendingRequeues);
            _ = RequeueAsync();

            async Task RequeueAsync()
            {
                try
                {
                    if (delay > TimeSpan.Zero) await Task.Delay(delay, _settings.DisposeToken).ConfigureAwait(false);

                    Enqueue(message);
                }
                catch (OperationCanceledException)
                {
                    // disposing
                }
                finally
                {
                    Interlocked.Decrement(ref _pendingRequeues);
                }
            }
        }

        private async Task RunAsync()
        {
            var token = _settings.DisposeToken;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!await _signal.WaitAsync(_settings.IdleTimeout, token).ConfigureAwait(false))
                    {
                        if (!await TryCloseIdleChannelAsync().ConfigureAwait(false)) continue;

                        Interlocked.Exchange(ref _running, 0);

                        // An Enqueue between the idle check and the reset above saw a running loop; take its message over.
                        if (_queue.IsEmpty || Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;

                        continue;
                    }

                    if (_queue.TryDequeue(out var message) && !message.CancellationToken.IsCancellationRequested)
                    {
                        await PublishAsync(message, token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception e)
                {
                    _settings.Logger.LogWarning(e, "Publish {Name} loop iteration failed", _name);
                }
            }
        }

        private async Task<bool> TryCloseIdleChannelAsync()
        {
            if (Volatile.Read(ref _pendingRequeues) > 0) return false;

            if (_current != null) await DisposeChannelAsync(_current).ConfigureAwait(false);
            _current = null;

            return true;
        }

        private async Task PublishAsync(PublisherConfirmMessage message, CancellationToken token)
        {
            message.RepublishCount++;
            if (message.RepublishCount > _settings.MaxRetries)
            {
                message.CompletionSource.TrySetException(new RetryLimitExceededException());

                return;
            }

            var channel = await GetChannelAsync(token).ConfigureAwait(false);
            var header = message.MessageHeader;
            var outcome = await PublishAndAwaitConfirmAsync(channel, message, token).ConfigureAwait(false);

            if (outcome.Confirmed)
            {
                message.CompletionSource.TrySetResult(true);
                await WaitForReplyAsync(header, token).ConfigureAwait(false);

                return;
            }

            if (outcome.CloseReason != null || outcome.Error != null || outcome.TimedOut) await RetireAsync(channel).ConfigureAwait(false);

            var refused = IsPublishRefusal(outcome.CloseReason);
            if (refused && _refusedTarget != null)
            {
                _settings.Logger.LogInformation(
                    "Publish refused: exchange={Exchange}, routingKey={RoutingKey}, messageId={MessageId}, reason={ReplyCode}:{ReplyText}",
                    header.Exchange,
                    header.RoutingKey,
                    header.MessageId,
                    outcome.CloseReason!.ReplyCode,
                    outcome.CloseReason.ReplyText);
                _refusedTarget.EnqueueAfter(message, _settings.RetryInterval);

                return;
            }

            if (outcome.Error != null) _settings.Logger.LogWarning(outcome.Error, "Publish on the {Name} channel failed", _name);

            EnqueueAfter(message, outcome.TimedOut ? TimeSpan.Zero : _settings.RetryInterval);
        }

        private async Task<Outcome> PublishAndAwaitConfirmAsync(IChannel channel, PublisherConfirmMessage message, CancellationToken token)
        {
            var header = message.MessageHeader;
            try
            {
                var attempt = new Attempt(channel, await channel.GetNextPublishSequenceNumberAsync(token).ConfigureAwait(false));
                Volatile.Write(ref _attempt, attempt);
                await channel.BasicPublishAsync(header.Exchange, header.RoutingKey, false, message.BasicProperties, message.Payload, token)
                    .ConfigureAwait(false);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                var delay = Task.Delay(_settings.RetryInterval, timeout.Token);
                var completed = await Task.WhenAny(attempt.Confirm.Task, delay).ConfigureAwait(false);
#if NET
                await timeout.CancelAsync().ConfigureAwait(false);
#else
                timeout.Cancel();
#endif
                if (completed != attempt.Confirm.Task)
                {
                    token.ThrowIfCancellationRequested();

                    return Outcome.Timeout;
                }

                return await attempt.Confirm.Task.ConfigureAwait(false) ? Outcome.Ack : Outcome.Nack;
            }
            catch (OperationInterruptedException e)
            {
                return new Outcome(false, false, e.ShutdownReason, null);
            }
            catch (Exception e) when (e is not OperationCanceledException || !token.IsCancellationRequested)
            {
                return new Outcome(false, false, null, e);
            }
            finally
            {
                Volatile.Write(ref _attempt, null);
            }
        }

        private async Task<IChannel> GetChannelAsync(CancellationToken token)
        {
            while (_current is not { IsOpen: true })
            {
                if (_current != null) await DisposeChannelAsync(_current).ConfigureAwait(false);
                _current = null;

                IChannel? channel = null;
                try
                {
                    channel = await _settings.Connection.CreateChannelAsync(_settings.ChannelOptions, token).ConfigureAwait(false);
                    var created = channel;
                    created.BasicAcksAsync += (_, e) => OnConfirmAsync(created, e.DeliveryTag, e.Multiple, true);
                    created.BasicNacksAsync += (_, e) => OnConfirmAsync(created, e.DeliveryTag, e.Multiple, false);
                    created.ChannelShutdownAsync += (_, e) => OnShutdownAsync(created, e);
                    await _settings.OnChannelCreated(created).ConfigureAwait(false);
                    _current = created;
                    _settings.Logger.LogDebug("Publish {Name} channel {ChannelNumber} opened", _name, created.ChannelNumber);
                }
                catch (Exception e)
                {
                    if (channel != null) await DisposeChannelAsync(channel).ConfigureAwait(false);
                    if (e is OperationCanceledException && token.IsCancellationRequested) throw;

                    _settings.Logger.LogWarning(e, "Could not open the publish {Name} channel -> retry", _name);
                    await Task.Delay(_settings.NetworkRecoveryInterval, token).ConfigureAwait(false);
                }
            }

            return _current;
        }

        private async Task RetireAsync(IChannel channel)
        {
            if (_current == channel) _current = null;

            await DisposeChannelAsync(channel).ConfigureAwait(false);
        }

        // Ends with the reply, when it is no longer awaited (request cancelled or timed out), after IdleTimeout or on dispose.
        private async Task WaitForReplyAsync(CarrotHeader header, CancellationToken token)
        {
            var reply = _settings.WaitForReply(header);
            if (reply.IsCompleted) return;

            using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
            await Task.WhenAny(reply, Task.Delay(_settings.IdleTimeout, stop.Token)).ConfigureAwait(false);
#if NET
            await stop.CancelAsync().ConfigureAwait(false);
#else
            stop.Cancel();
#endif
            token.ThrowIfCancellationRequested();
        }

        private async Task DisposeChannelAsync(IChannel channel)
        {
            try
            {
                await channel.DisposeAsync().ConfigureAwait(false);
                _settings.Logger.LogDebug("Publish {Name} channel closed", _name);
            }
            catch (Exception e)
            {
                _settings.Logger.LogDebug(e, "Closing the publish {Name} channel failed", _name);
            }
        }

        private Task OnConfirmAsync(IChannel channel, ulong deliveryTag, bool multiple, bool ack)
        {
            var attempt = Volatile.Read(ref _attempt);
            if (attempt != null && attempt.Channel == channel && (deliveryTag == attempt.SeqNo || (multiple && deliveryTag > attempt.SeqNo)))
            {
                attempt.Confirm.TrySetResult(ack);
            }

            return Task.CompletedTask;
        }

        private Task OnShutdownAsync(IChannel channel, ShutdownEventArgs e)
        {
            var attempt = Volatile.Read(ref _attempt);
            if (attempt != null && attempt.Channel == channel) attempt.Confirm.TrySetException(new AlreadyClosedException(e));

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// The one message in flight; events of another channel instance cannot complete it.
    /// </summary>
    private sealed class Attempt
    {
        public Attempt(IChannel channel, ulong seqNo)
        {
            Channel = channel;
            SeqNo = seqNo;
        }

        public IChannel Channel { get; }

        public ulong SeqNo { get; }

        public TaskCompletionSource<bool> Confirm { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class Outcome
    {
        public static readonly Outcome Ack = new(true, false, null, null);
        public static readonly Outcome Nack = new(false, false, null, null);
        public static readonly Outcome Timeout = new(false, true, null, null);

        public Outcome(bool confirmed, bool timedOut, ShutdownEventArgs? closeReason, Exception? error)
        {
            Confirmed = confirmed;
            TimedOut = timedOut;
            CloseReason = closeReason;
            Error = error;
        }

        public bool Confirmed { get; }

        public bool TimedOut { get; }

        public ShutdownEventArgs? CloseReason { get; }

        public Exception? Error { get; }
    }
}