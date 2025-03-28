using CarrotMQ.Core.Common;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Serialization;
using CarrotMQ.RabbitMQ.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        _connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>())
            .Returns(
                _ =>
                {
                    _channelDeliveryTagCounter = 0;

                    return _channel;
                });

        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _dateTimeProvider.Now.Returns(_dateTimeNow);

        _publisherConfirmChannel = await PublisherConfirmChannel.CreateAsync(
                _connection,
                TimeSpan.FromSeconds(2),
                _publisherConfirmOptions,
                new ProtocolSerializer(),
                new BasicPropertiesMapper(),
                TestLoggerFactory.Instance,
                _intervalTimer,
                _dateTimeProvider) as PublisherConfirmChannel
            ?? throw new InvalidOperationException();

        await VerifyInitialCallsAsync().ConfigureAwait(false);

        await FireRepublishTimerEventAsync().ConfigureAwait(false); // Trigger timer for first OnTimedEvent (avoid "republishAll" call)
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

    [DataTestMethod]
    [DataRow(2, 1, 1)]
    [DataRow(2, 1, 2)]
    [DataRow(3, 0, 1)]
    [DataRow(3, 0, 2)]
    [DataRow(3, 0, 3)]
    [DataRow(3, 2, 2)]
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

        await Assert.ThrowsExceptionAsync<RetryLimitExceededException>(() => task).ConfigureAwait(false);

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
        Parallel.For(1, noOfMessages + 1, index => AckMessage((ulong)index));

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
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => task).ConfigureAwait(false);

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
        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => Task.WhenAll(tasks)).ConfigureAwait(false);

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
        _channel.When(
                m => m.BasicPublishAsync(
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

    private Task[] PublishAsync(int noOfMessages, CancellationToken token)
    {
        if (noOfMessages <= 0) return [];

        var publishingTasks = new List<Task>();

        var publishedCount = 0;
        using var allPublishedEvent = new AutoResetEvent(false);

        _channel.When(
                async m => await m.BasicPublishAsync(
                        Arg.Any<string>(),
                        Arg.Any<string>(),
                        Arg.Any<bool>(),
                        Arg.Any<BasicProperties>(),
                        Arg.Any<ReadOnlyMemory<byte>>(),
                        Arg.Any<CancellationToken>())
                    .ConfigureAwait(false))
            .Do(
                _ =>
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

    private void FireChannelShutdown()
    {
        using var resetEvent = new ManualResetEventSlim();

        var isOpenCalledCounter = 0;
        _channel.IsOpen
            .Returns(
                _ =>
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