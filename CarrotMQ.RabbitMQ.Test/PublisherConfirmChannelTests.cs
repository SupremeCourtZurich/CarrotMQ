using CarrotMQ.Core.Common;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Serialization;
using CarrotMQ.RabbitMQ.Test.Helper;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
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

    [TestInitialize]
    public async Task Initialize()
    {
        _publisherConfirmOptions.RetryLimit = 5;
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
        _connection.IsOpen.Returns(true);
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
        await _publisherConfirmChannel.DisposeAsync().ConfigureAwait(false);
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
        await Assert.ThrowsExactlyAsync<PublishFailedException>(() => task).ConfigureAwait(false);

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
        await Assert.ThrowsExactlyAsync<PublishFailedException>(() => Task.WhenAll(tasks)).ConfigureAwait(false);

        // Republish last message
        await RepublishAsync(1).ConfigureAwait(false);

        // verify
        Assert.IsTrue(tasks.All(t => t.Exception?.InnerException is PublishFailedException { InnerException: OperationCanceledException }));
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
        _channel.When(m => _ = m.BasicPublishAsync(
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
    public async Task RetryLimitReachedWithoutAClose_FailsWithAnUnknownReason()
    {
        // Arrange
        var task = PublishAsync(1, CancellationToken.None).Single();

        // Act
        await RepublishAsync(_publisherConfirmOptions.RetryLimit + 1).ConfigureAwait(false);

        // Assert
        var exception = await Assert.ThrowsAsync<RetryLimitExceededException>(() => task).ConfigureAwait(false);
        Assert.AreEqual(PublishFailureReason.Unknown, exception.Reason);
        Assert.IsNull(exception.InnerException);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task RetryLimitReachedByAConnectionLevelClose_FailsAsUnreachable()
    {
        // Arrange
        var task = PublishAsync(1, CancellationToken.None).Single();
        await RepublishAsync(_publisherConfirmOptions.RetryLimit).ConfigureAwait(false);
        CloseChannel(new ShutdownEventArgs(ShutdownInitiator.Library, 541, "Unexpected"));
        await WaitForCallsAsync(_connection, nameof(IConnection.CreateChannelAsync), 2).ConfigureAwait(false);

        // Act
        await FireRepublishTimerEventAsync().ConfigureAwait(false);

        // Assert
        var exception = await Assert.ThrowsAsync<RetryLimitExceededException>(() => task).ConfigureAwait(false);
        Assert.AreEqual(PublishFailureReason.Unreachable, exception.Reason);
        Assert.IsInstanceOfType<AlreadyClosedException>(exception.InnerException);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task RetryLimitReachedByARefusalClose_FailsWithAnUnknownReason()
    {
        // Arrange
        var task = PublishAsync(1, CancellationToken.None).Single();
        await RepublishAsync(_publisherConfirmOptions.RetryLimit).ConfigureAwait(false);
        RefuseMainChannel();
        await WaitForCallsAsync(_connection, nameof(IConnection.CreateChannelAsync), 2).ConfigureAwait(false);

        // Act
        await FireRepublishTimerEventAsync().ConfigureAwait(false);

        // Assert
        var exception = await Assert.ThrowsAsync<RetryLimitExceededException>(() => task).ConfigureAwait(false);
        Assert.AreEqual(PublishFailureReason.Unknown, exception.Reason);
        Assert.IsInstanceOfType<AlreadyClosedException>(exception.InnerException);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task TimeoutsAfterAConnectionLevelClose_RetryLimitFailsWithAnUnknownReason()
    {
        // Arrange
        var task = PublishAsync(1, CancellationToken.None).Single();
        CloseChannel(new ShutdownEventArgs(ShutdownInitiator.Library, 541, "Unexpected"));
        await WaitForCallsAsync(_connection, nameof(IConnection.CreateChannelAsync), 2).ConfigureAwait(false);

        // Act
        await RepublishAsync(_publisherConfirmOptions.RetryLimit + 1).ConfigureAwait(false);

        // Assert
        var exception = await Assert.ThrowsAsync<RetryLimitExceededException>(() => task).ConfigureAwait(false);
        Assert.AreEqual(PublishFailureReason.Unknown, exception.Reason);
        Assert.IsNull(exception.InnerException);
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task BrokerUnreachableWhileRecovering_TokenCancelled_FailsAsUnreachable()
    {
        // Arrange
        await MakeBrokerUnreachableAsync().ConfigureAwait(false);
        using var cts = new CancellationTokenSource();
        var publish = _publisherConfirmChannel.PublishAsync(new CarrotMessage(new CarrotHeader(), string.Empty), cts.Token);

        try
        {
            // Act
            cts.Cancel();

            // Assert
            await Wait.UntilAsync(() => publish.IsCompleted, "the cancelled publish completes while the broker is unreachable")
                .ConfigureAwait(false);
            await AssertCancelledAsync(publish, PublishFailureReason.Unreachable).ConfigureAwait(false);
        }
        finally
        {
            MakeBrokerReachable();
        }
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task ConnectionBackWhileTheChannelIsStillRecovering_TokenCancelled_FailsAsUnreachable()
    {
        // Arrange
        await MakeBrokerUnreachableAsync().ConfigureAwait(false);
        _connection.IsOpen.Returns(true);
        using var cts = new CancellationTokenSource();
        var publish = PublishOneAsync(cts.Token);

        try
        {
            // Act
            cts.Cancel();

            // Assert
            await Wait.UntilAsync(() => publish.IsCompleted, "the cancelled publish completes while the channel is reopened")
                .ConfigureAwait(false);
            await AssertCancelledAsync(publish, PublishFailureReason.Unreachable).ConfigureAwait(false);
        }
        finally
        {
            MakeBrokerReachable();
        }
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task TokenCancelledWhileWaitingForTheConfirm_FailsWithAnUnknownReason()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var task = PublishAsync(1, cts.Token).Single();

        // Act
        cts.Cancel();

        // Assert
        await AssertCancelledAsync(task, PublishFailureReason.Unknown).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task TokenCancelledAfterTheConnectionWasLostAndRecovered_FailsAsUnreachable()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var task = PublishAsync(1, cts.Token).Single();
        CloseChannel(new ShutdownEventArgs(ShutdownInitiator.Library, 541, "Unexpected"));
        await WaitForCallsAsync(_connection, nameof(IConnection.CreateChannelAsync), 2).ConfigureAwait(false);
        await FireRepublishTimerEventAsync().ConfigureAwait(false);

        // Act
        cts.Cancel();

        // Assert
        await AssertCancelledAsync(task, PublishFailureReason.Unreachable).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task MaxConcurrentConfirmsReached_TokenCancelled_FailsWithAnUnknownReason()
    {
        // Arrange
        await RecreateChannelAsync(options => options.MaxConcurrentConfirms = 1).ConfigureAwait(false);
        PublishAsync(1, CancellationToken.None);
        using var cts = new CancellationTokenSource();
        var waiting = PublishOneAsync(cts.Token);

        // Act
        cts.Cancel();

        // Assert
        await AssertCancelledAsync(waiting, PublishFailureReason.Unknown).ConfigureAwait(false);
    }

    private static async Task AssertCancelledAsync(Task publish, PublishFailureReason reason)
    {
        var exception = await Assert.ThrowsExactlyAsync<PublishFailedException>(() => publish).ConfigureAwait(false);
        Assert.AreEqual(reason, exception.Reason);
        Assert.IsInstanceOfType<OperationCanceledException>(exception.InnerException);
    }

    private static Task WaitForCallsAsync<T>(T substitute, string methodName, int count)
        where T : class
    {
        return Wait.UntilAsync(
            () => substitute.ReceivedCalls().Count(c => string.Equals(c.GetMethodInfo().Name, methodName, StringComparison.Ordinal)) >= count,
            $"{methodName} called {count} times");
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

    // Closes the main channel like a lost connection; the recovery loop keeps failing to reopen it and holds ChannelLock.
    private async Task MakeBrokerUnreachableAsync()
    {
        _connection.IsOpen.Returns(false);
        _channel.IsOpen.Returns(false);
        _connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>(), Arg.Any<CancellationToken>())
            .Returns<IChannel>(_ => throw new BrokerUnreachableException(new InvalidOperationException("broker down")));
        var opensBefore = _connection.ReceivedCalls()
            .Count(c => string.Equals(c.GetMethodInfo().Name, nameof(IConnection.CreateChannelAsync), StringComparison.Ordinal));
        _channel.ChannelShutdownAsync += Raise.Event<ShutdownAsyncEventHandler>(null, new ShutdownEventArgs(ShutdownInitiator.Library, 541, "Unexpected"));
        await WaitForCallsAsync(_connection, nameof(IConnection.CreateChannelAsync), opensBefore + 1).ConfigureAwait(false);
    }

    private void MakeBrokerReachable()
    {
        _connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>(), Arg.Any<CancellationToken>()).Returns(_channel);
        _channel.IsOpen.Returns(true);
        _connection.IsOpen.Returns(true);
    }

    private Task PublishOneAsync(CancellationToken token = default)
    {
        return _publisherConfirmChannel.PublishAsync(new CarrotMessage(new CarrotHeader(), string.Empty), token);
    }

    private void RefuseMainChannel()
    {
        CloseChannel(Refusal);
    }

    private async Task RecreateChannelAsync(Action<PublisherConfirmOptions> configure)
    {
        await _publisherConfirmChannel.DisposeAsync().ConfigureAwait(false);
        configure(_publisherConfirmOptions);
        _publisherConfirmChannel = await CreatePublisherConfirmChannelAsync().ConfigureAwait(false);
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

    // Each round advances the clock past RetryIntervalInMs; roundsDone continues after earlier rounds.
    private async Task RepublishAsync(int times, int roundsDone = 0)
    {
        var dateTimeNow = _dateTimeNow.AddMilliseconds(roundsDone * (_publisherConfirmOptions.RetryIntervalInMs + 1.0));
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
