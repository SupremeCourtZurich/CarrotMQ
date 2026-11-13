using CarrotMQ.Core.Common;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Serialization;
using CarrotMQ.RabbitMQ.Test.Helper;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using AckAsyncEventHandler = RabbitMQ.Client.Events.AsyncEventHandler<RabbitMQ.Client.Events.BasicAckEventArgs>;
using NAckAsyncEventHandler = RabbitMQ.Client.Events.AsyncEventHandler<RabbitMQ.Client.Events.BasicNackEventArgs>;
using ShutdownAsyncEventHandler = RabbitMQ.Client.Events.AsyncEventHandler<RabbitMQ.Client.Events.ShutdownEventArgs>;

// ReSharper disable AccessToDisposedClosure

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class PublisherConfirmChannelTests
{
    private static readonly ShutdownEventArgs Refusal = new(ShutdownInitiator.Peer, 404, "NOT_FOUND", 60, 40);
    private readonly DateTime _dateTimeNow = new(2023, 05, 12, 11, 29, 59);
    private readonly PublisherConfirmOptions _publisherConfirmOptions = new();
    private IConnection _connection = null!;
    private IChannel _channel = null!;
    private IDateTimeProvider _dateTimeProvider = null!;
    private readonly TestIntervalTimer _intervalTimer = new();
    private IBasicProperties _basicProperties = null!;

#if NET
    private ulong _channelDeliveryTagCounter;
#else
    private int _channelDeliveryTagCounter;
#endif
    private PublisherConfirmChannel _publisherConfirmChannel = null!;
    private bool _disposed;

    [TestInitialize]
    public async Task Initialize()
    {
        _publisherConfirmOptions.RetryLimit = 5;
        _publisherConfirmOptions.RecoveryChannelIdleTimeoutInMs = 200;
        _basicProperties = Substitute.For<IBasicProperties>();
        _basicProperties.DeliveryMode.Returns(DeliveryModes.Transient);

        _channel = Substitute.For<IChannel>();
        _channel.IsOpen.Returns(true);

#if NET
        _channel.GetNextPublishSequenceNumberAsync(Arg.Any<CancellationToken>()).Returns(_ => Interlocked.Increment(ref _channelDeliveryTagCounter));
#else
        _channel.GetNextPublishSequenceNumberAsync(Arg.Any<CancellationToken>()).Returns(_ => (ulong)Interlocked.Increment(ref _channelDeliveryTagCounter));
#endif

        _connection = Substitute.For<IConnection>();
        _connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>())
            .Returns(_ =>
            {
                _channelDeliveryTagCounter = 0;

                return _channel;
            });

        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _dateTimeProvider.Now.Returns(_dateTimeNow);

        _publisherConfirmChannel = await CreatePublisherConfirmChannelAsync().ConfigureAwait(false);

        await VerifyInitialCallsAsync().ConfigureAwait(false);

        await FireRepublishTimerEventAsync().ConfigureAwait(false); // Trigger timer for first OnTimedEvent (avoid "republishAll" call)
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (!_disposed) await _publisherConfirmChannel.DisposeAsync().ConfigureAwait(false);
    }

    [TestMethod]
    public async Task SingleAcknowledgments()
    {
        PublishAsync(5, CancellationToken.None);

        AckMessage(1);
        AckMessage(4);
        NAckMessage(3);
        NAckMessage(2);

        // Republish NAck messages
        await FireRepublishTimerEventAsync().ConfigureAwait(false);

        await VerifyBasicPublishAsync(7).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task MultipleAcknowledgments()
    {
        PublishAsync(5, CancellationToken.None);

        NAckMessage(2, true);
        AckMessage(4, true);

        // Republish NAck messages
        await FireRepublishTimerEventAsync().ConfigureAwait(false);

        await VerifyBasicPublishAsync(7).ConfigureAwait(false);
    }

    [DataRow(2, 1, 1)]
    [DataRow(2, 1, 2)]
    [DataRow(3, 0, 1)]
    [DataRow(3, 0, 2)]
    [DataRow(3, 0, 3)]
    [DataRow(3, 2, 2)]
    [TestMethod]
    public async Task Republish(int oldMsgs, int newMsgs, int ackSeqNo)
    {
        // 1) Publish messages
        PublishAsync(oldMsgs, CancellationToken.None);
        _dateTimeProvider.Now.Returns(_dateTimeNow.AddMilliseconds(50));
        PublishAsync(newMsgs, CancellationToken.None);

        // 2) ack one of the old messages
        AckMessage((ulong)ackSeqNo);

        // 3) republish old events
        await RepublishAsync(1).ConfigureAwait(false);

        // verify
        var republishedMsgs = oldMsgs - (ackSeqNo == 0 ? 0 : 1);
        await VerifyBasicPublishAsync(oldMsgs + newMsgs + republishedMsgs).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task RepublishMaxRetries()
    {
        var task = PublishAsync(1, CancellationToken.None).First();
        await RepublishAsync(_publisherConfirmOptions.RetryLimit);
        AckMessage(6);

        await task.ConfigureAwait(false);

        await VerifyBasicPublishAsync(6).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task RepublishWithRetryLimitExceededException()
    {
        var task = PublishAsync(1, CancellationToken.None).First();
        await RepublishAsync(_publisherConfirmOptions.RetryLimit + 1);

        await Assert.ThrowsAsync<RetryLimitExceededException>(() => task).ConfigureAwait(false);

        await VerifyBasicPublishAsync(6).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ParallelAcks()
    {
        int noOfMessages = _publisherConfirmOptions.MaxConcurrentConfirms;

        // 1) Publish messages
        var tasks = PublishAsync(noOfMessages, CancellationToken.None);

        // 2) AckAsync all messages
        AckMessagesInParallel(noOfMessages);

        // verify
        await VerifyBasicPublishAsync(noOfMessages).ConfigureAwait(false);

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Cancel()
    {
        // 1) Publish message with cancellation token
        using var tokenSource = new CancellationTokenSource();
        var task = PublishAsync(1, tokenSource.Token).First();

        // 2) Cancel publish
        tokenSource.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(() => task).ConfigureAwait(false);

        // Nothing should be republished
        await RepublishAsync(1).ConfigureAwait(false);

        // verify
        await VerifyBasicPublishAsync(1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task CancelSeveralMessages()
    {
        // 1) Publish 3 messages with cancellation token
        using var tokenSource = new CancellationTokenSource();
        var tasks = PublishAsync(3, tokenSource.Token);

        // 2. Publish 1 message without cancellation token
        PublishAsync(1, CancellationToken.None);

        // 3) Cancel first 3 publishes
        tokenSource.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(() => Task.WhenAll(tasks)).ConfigureAwait(false);

        // Republish last message
        await RepublishAsync(1).ConfigureAwait(false);

        // verify
        Assert.IsTrue(tasks.All(t => t.IsCanceled));
        await VerifyBasicPublishAsync(5).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task Reset()
    {
        // 1) Publish 2 messages
        PublishAsync(2, CancellationToken.None);

        // 2) Reset channel
        FireChannelShutdown();

        // 3) Republish 2 messages
        await FireRepublishTimerEventAsync().ConfigureAwait(false);

        // verify
        await VerifyBasicPublishAsync(4).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ResetWithAdditionalPublish()
    {
        // 1) Publish 2 messages
        PublishAsync(2, CancellationToken.None);

        // 2) Reset channel
        FireChannelShutdown();

        // 4) Publish new message
        PublishAsync(1, CancellationToken.None);

        // 5) Republish 2 messages (from step 1)
        await FireRepublishTimerEventAsync().ConfigureAwait(false);

        // verify
        await VerifyBasicPublishAsync(5).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task MaxConcurrentConfirms()
    {
        int noOfMessages = _publisherConfirmOptions.MaxConcurrentConfirms;

        // 1) Publish max messages
        PublishAsync(noOfMessages, CancellationToken.None);

        // 2) Try Publish 1 message
        using var publishEvent = new AutoResetEvent(false);
        _channel.When(m => m.BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>()))
            .Do(_ => { publishEvent.Set(); });
        var message = new CarrotMessage(new CarrotHeader(), string.Empty);

        var unused = _publisherConfirmChannel.PublishAsync(message, CancellationToken.None);
        await Task.Delay(50).ConfigureAwait(false);

        // 3) Verify max messages are published
        await VerifyBasicPublishAsync(noOfMessages).ConfigureAwait(false);

        // 4) AckAsync 1 -> last message (step 2) is now published
        AckMessage(23);
        publishEvent.WaitOne();

        // 5) Verify MaxConcurrentConfirms+1 messages are published
        await VerifyBasicPublishAsync(noOfMessages + 1).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task PublishRefused_OutstandingMessagesArePublishedOnTheRecoveryChannelOneAtATime()
    {
        // Arrange
        var recoveryChannel = UseRecoveryChannel();
        var tasks = PublishAsync(2, CancellationToken.None);

        // Act
        RefuseMainChannel();
        recoveryChannel.Ack(await recoveryChannel.WaitForPublishAsync(1).ConfigureAwait(false));
        recoveryChannel.Ack(await recoveryChannel.WaitForPublishAsync(2).ConfigureAwait(false));
        await Task.WhenAll(tasks).ConfigureAwait(false);
        await FireRepublishTimerEventAsync().ConfigureAwait(false);

        // Assert
        recoveryChannel.AssertLog("publish 1", "ack 1", "publish 2", "ack 2");
        await VerifyBasicPublishAsync(2).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task PublishRefusedOnTheRecoveryChannel_OnlyTheRefusedMessageIsRetried()
    {
        // Arrange
        var recoveryChannel = UseRecoveryChannel();
        var tasks = PublishAsync(2, CancellationToken.None);

        // Act
        RefuseMainChannel();
        recoveryChannel.Refuse(await recoveryChannel.WaitForPublishAsync(1).ConfigureAwait(false));
        recoveryChannel.Ack(await recoveryChannel.WaitForPublishAsync(2).ConfigureAwait(false));
        await tasks[1].ConfigureAwait(false);
        recoveryChannel.Ack(await recoveryChannel.WaitForPublishAsync(3).ConfigureAwait(false));
        await tasks[0].ConfigureAwait(false);

        // Assert
        recoveryChannel.AssertLog("publish 1", "refuse 1", "publish 2", "ack 2", "publish 3", "ack 3");
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task PublishRefusedAfterRepublishes_RetryLimitCountsTheEarlierAttempts()
    {
        // Arrange
        var recoveryChannel = UseRecoveryChannel();
        recoveryChannel.RefuseEveryPublish();
        var task = PublishAsync(1, CancellationToken.None).Single();
        await RepublishAsync(2).ConfigureAwait(false);

        // Act
        RefuseMainChannel();
        await Assert.ThrowsAsync<RetryLimitExceededException>(() => task).ConfigureAwait(false);

        // Assert
        recoveryChannel.AssertPublishes(_publisherConfirmOptions.RetryLimit - 2);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task RecoveryChannelIdle_IsClosedAfterRecoveryChannelIdleTimeout()
    {
        // Arrange
        await RecreateChannelAsync(options => options.RetryIntervalInMs = 60_000).ConfigureAwait(false);
        var recoveryChannel = UseRecoveryChannel();
        var tasks = PublishAsync(1, CancellationToken.None);

        // Act
        RefuseMainChannel();
        recoveryChannel.Ack(await recoveryChannel.WaitForPublishAsync(1).ConfigureAwait(false));
        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Assert
        await recoveryChannel.WaitForDisposeAsync(1).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task DisposedWhileRecovering_RecoveryChannelIsDisposed()
    {
        // Arrange
        var recoveryChannel = UseRecoveryChannel();
        PublishAsync(1, CancellationToken.None);
        RefuseMainChannel();
        await recoveryChannel.WaitForPublishAsync(1).ConfigureAwait(false);

        // Act
        _disposed = true;
        await _publisherConfirmChannel.DisposeAsync().ConfigureAwait(false);

        // Assert
        await recoveryChannel.Channel.Received(1).DisposeAsync().ConfigureAwait(false);
        await VerifyRecoveryChannelOpensAsync(1).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task RecoveryChannelClosedWhenIdle_IsReopenedOnTheNextRefusal()
    {
        // Arrange
        var recoveryChannel = UseRecoveryChannel();
        var first = PublishAsync(1, CancellationToken.None);
        RefuseMainChannel();
        recoveryChannel.Ack(await recoveryChannel.WaitForPublishAsync(1).ConfigureAwait(false));
        await Task.WhenAll(first).ConfigureAwait(false);
        await recoveryChannel.WaitForDisposeAsync(1).ConfigureAwait(false);

        // Act
        var second = PublishAsync(1, CancellationToken.None);
        RefuseMainChannel();
        recoveryChannel.Ack(await recoveryChannel.WaitForPublishAsync(2).ConfigureAwait(false));
        await Task.WhenAll(second).ConfigureAwait(false);

        // Assert
        await VerifyRecoveryChannelOpensAsync(2).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ChannelClosedWithoutRefusal_NoRecoveryChannelIsCreated()
    {
        // Arrange
        PublishAsync(2, CancellationToken.None);

        // Act
        CloseChannel(new ShutdownEventArgs(ShutdownInitiator.Library, 541, "Unexpected"));
        await WaitForCallsAsync(_connection, nameof(IConnection.CreateChannelAsync), 2).ConfigureAwait(false);
        await FireRepublishTimerEventAsync().ConfigureAwait(false);

        // Assert
        await VerifyBasicPublishAsync(4).ConfigureAwait(false);
        await VerifyRecoveryChannelOpensAsync(0).ConfigureAwait(false);
    }

    private static async Task WaitForCallsAsync<T>(T substitute, string methodName, int count)
        where T : class
    {
        var deadline = DateTime.UtcNow.AddSeconds(4);
        while (substitute.ReceivedCalls().Count(c => string.Equals(c.GetMethodInfo().Name, methodName, StringComparison.Ordinal)) < count)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"{methodName} not called {count} times within 4 s");

            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    private async Task<PublisherConfirmChannel> CreatePublisherConfirmChannelAsync()
    {
        return await PublisherConfirmChannel.CreateAsync(
                _connection,
                TimeSpan.FromSeconds(2),
                _publisherConfirmOptions,
                new ProtocolSerializer(),
                TestLoggerFactory.Instance,
                _intervalTimer,
                _dateTimeProvider)
            .ConfigureAwait(false) as PublisherConfirmChannel
            ?? throw new InvalidOperationException();
    }

    private void RefuseMainChannel()
    {
        CloseChannel(Refusal);
    }

    private RecoveryChannel UseRecoveryChannel()
    {
        var recoveryChannel = new RecoveryChannel();

        // The recovery channel is opened with its cancellable dispose token, the main channel without a token.
        _connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                if (ci.ArgAt<CancellationToken>(1).CanBeCanceled) return recoveryChannel.Channel;

                _channelDeliveryTagCounter = 0;

                return _channel;
            });

        return recoveryChannel;
    }

    private async Task VerifyRecoveryChannelOpensAsync(int times)
    {
        await _connection.Received(times)
            .CreateChannelAsync(Arg.Any<CreateChannelOptions>(), Arg.Is<CancellationToken>(t => t.CanBeCanceled))
            .ConfigureAwait(false);
    }

    private async Task RecreateChannelAsync(Action<PublisherConfirmOptions> configure)
    {
        await _publisherConfirmChannel.DisposeAsync().ConfigureAwait(false);
        configure(_publisherConfirmOptions);
        _publisherConfirmChannel = await CreatePublisherConfirmChannelAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The channel every recovery and quarantine publish goes to; acks and refusals are recorded in order.
    /// </summary>
    private sealed class RecoveryChannel
    {
        private readonly object _lock = new();
        private readonly List<string> _log = [];
        private bool _refuseEveryPublish;
        private long _seqNo;

        public RecoveryChannel()
        {
            Channel = Substitute.For<IChannel>();
            Channel.IsOpen.Returns(true);
            Channel.GetNextPublishSequenceNumberAsync(Arg.Any<CancellationToken>()).Returns(_ => (ulong)Interlocked.Increment(ref _seqNo));
            Channel.When(async c => await c.BasicPublishAsync(
                        Arg.Any<string>(),
                        Arg.Any<string>(),
                        Arg.Any<bool>(),
                        Arg.Any<BasicProperties>(),
                        Arg.Any<ReadOnlyMemory<byte>>(),
                        Arg.Any<CancellationToken>())
                    .ConfigureAwait(false))
                .Do(_ =>
                {
                    var seqNo = (ulong)Interlocked.Read(ref _seqNo);
                    Record($"publish {seqNo}");
                    if (_refuseEveryPublish) Refuse(seqNo);
                });
        }

        public IChannel Channel { get; }

        public void RefuseEveryPublish()
        {
            _refuseEveryPublish = true;
        }

        public void Ack(ulong seqNo)
        {
            Record($"ack {seqNo}");
            Channel.BasicAcksAsync += Raise.Event<AckAsyncEventHandler>(Channel, new BasicAckEventArgs(seqNo, false));
        }

        public void Refuse(ulong seqNo)
        {
            Record($"refuse {seqNo}");
            Channel.ChannelShutdownAsync += Raise.Event<ShutdownAsyncEventHandler>(Channel, Refusal);
        }

        public async Task<ulong> WaitForPublishAsync(int count)
        {
            await WaitForCallsAsync(Channel, nameof(IChannel.BasicPublishAsync), count).ConfigureAwait(false);

            return (ulong)count;
        }

        public Task WaitForDisposeAsync(int count)
        {
            return WaitForCallsAsync(Channel, nameof(IChannel.DisposeAsync), count);
        }

        public void AssertPublishes(int count)
        {
            Assert.AreEqual(count, Snapshot().Count(e => e.StartsWith("publish ", StringComparison.Ordinal)));
        }

        public void AssertLog(params string[] expected)
        {
            CollectionAssert.AreEqual(expected, Snapshot(), string.Join(", ", Snapshot()));
        }

        private void Record(string entry)
        {
            lock (_lock)
            {
                _log.Add(entry);
            }
        }

        private List<string> Snapshot()
        {
            lock (_lock)
            {
                return [.. _log];
            }
        }
    }

    private Task[] PublishAsync(int noOfMessages, CancellationToken token)
    {
        if (noOfMessages <= 0) return [];

        var publishingTasks = new List<Task>();

        var publishedCount = 0;
        using var allPublishedEvent = new AutoResetEvent(false);

        _channel.When(async m => await m.BasicPublishAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Any<BasicProperties>(),
                    Arg.Any<ReadOnlyMemory<byte>>(),
                    Arg.Any<CancellationToken>())
                .ConfigureAwait(false))
            .Do(_ =>
            {
                if (Interlocked.Increment(ref publishedCount) == noOfMessages) allPublishedEvent.Set();
            });

        for (var i = 0; i < noOfMessages; i++)
        {
            var message = new CarrotMessage(new CarrotHeader(), string.Empty);

            var task = _publisherConfirmChannel.PublishAsync(message, token);
            publishingTasks.Add(task);
        }

        allPublishedEvent.WaitOne();

        return [.. publishingTasks];
    }

    private async Task RepublishAsync(int times)
    {
        var dateTimeNow = _dateTimeNow;
        for (var i = 0; i < times; i++)
        {
            dateTimeNow = dateTimeNow.AddMilliseconds(_publisherConfirmOptions.RetryIntervalInMs + 1);
            _dateTimeProvider.Now.Returns(dateTimeNow);
            await FireRepublishTimerEventAsync().ConfigureAwait(false);
        }
    }

    private async Task FireRepublishTimerEventAsync()
    {
        await _intervalTimer.FireTimedEvent(new IIntervalTimer.IntervalTimerElapsedEventArgs()).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes the channel and lets exactly one recovery reopen it (recovery checks <c>IsOpen</c> twice).
    /// </summary>
    private void CloseChannel(ShutdownEventArgs shutdownEventArgs)
    {
        var closedChecks = 2;
        _channel.IsOpen.Returns(_ => Interlocked.Decrement(ref closedChecks) < 0);
        _channel.ChannelShutdownAsync += Raise.Event<ShutdownAsyncEventHandler>(null, shutdownEventArgs);
    }

    private void FireChannelShutdown()
    {
        using var resetEvent = new ManualResetEventSlim();

        var isOpenCalledCounter = 0;
        _channel.IsOpen
            .Returns(_ =>
            {
                if (isOpenCalledCounter++ >= 2)
                {
                    resetEvent.Set();
                }

                return false;
            });

        _channel.ChannelShutdownAsync += Raise.Event<ShutdownAsyncEventHandler>(
            null,
            new ShutdownEventArgs(ShutdownInitiator.Application, 400, ""));
        resetEvent.Wait(); // wait on IsOpen call

        _channel.IsOpen.Returns(true);
    }

    private async Task VerifyBasicPublishAsync(int times)
    {
        await _channel.Received(times)
            .BasicPublishAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>())
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Acknowledges <paramref name="noOfMessages" /> messages concurrently from dedicated threads.
    /// NSubstitute blocks the raising thread until the async event handler completes, so the raising
    /// threads must stay off the thread pool — the handler continuations need those threads.
    /// </summary>
    private void AckMessagesInParallel(int noOfMessages)
    {
        const int parallelism = 8;

        var threads = Enumerable.Range(0, parallelism)
            .Select(
                offset => new Thread(
                    () =>
                    {
                        for (var deliveryTag = offset + 1; deliveryTag <= noOfMessages; deliveryTag += parallelism)
                        {
                            AckMessage((ulong)deliveryTag);
                        }
                    })
                {
                    IsBackground = true,
                })
            .ToArray();

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();
    }

    private void AckMessage(ulong deliveryTag, bool multiAck = false)
    {
        _channel.BasicAcksAsync +=
            Raise.Event<AckAsyncEventHandler>(this, new BasicAckEventArgs(deliveryTag, multiAck));
    }

    private void NAckMessage(ulong deliveryTag, bool multiAck = false)
    {
        _channel.BasicNacksAsync +=
            Raise.Event<NAckAsyncEventHandler>(this, new BasicNackEventArgs(deliveryTag, multiAck, false));
    }

    private async Task VerifyInitialCallsAsync()
    {
        await _connection.Received()
            .CreateChannelAsync(Arg.Is<CreateChannelOptions>(o => o.PublisherConfirmationsEnabled == true))
            .ConfigureAwait(false);
        _channel.Received().ChannelShutdownAsync += Arg.Any<ShutdownAsyncEventHandler>();
        _channel.Received().BasicAcksAsync += Arg.Any<AckAsyncEventHandler>();
        _channel.Received().BasicNacksAsync += Arg.Any<NAckAsyncEventHandler>();
    }
}