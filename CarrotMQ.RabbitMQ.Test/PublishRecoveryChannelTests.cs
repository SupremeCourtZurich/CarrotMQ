using System.Collections.Concurrent;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Test.Helper;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using AckAsyncEventHandler = RabbitMQ.Client.Events.AsyncEventHandler<RabbitMQ.Client.Events.BasicAckEventArgs>;
using NAckAsyncEventHandler = RabbitMQ.Client.Events.AsyncEventHandler<RabbitMQ.Client.Events.BasicNackEventArgs>;
using ShutdownAsyncEventHandler = RabbitMQ.Client.Events.AsyncEventHandler<RabbitMQ.Client.Events.ShutdownEventArgs>;

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class PublishRecoveryChannelTests
{
    private static readonly ShutdownEventArgs Refusal = new(ShutdownInitiator.Peer, 404, "NOT_FOUND", 60, 40);
    private static readonly ShutdownEventArgs ConnectionLost = new(ShutdownInitiator.Library, 541, "Unexpected");
    private static readonly TimeSpan Long = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Short = TimeSpan.FromMilliseconds(20);

    private readonly ConcurrentDictionary<Guid, Reply> _replies = new();
    private FakeBroker _broker = null!;
    private PublishRecoveryChannel? _sut;

    private enum Answer
    {
        Ack,
        Nack,
        Refuse,
        LoseConnection,
        None
    }

    [TestInitialize]
    public void Initialize()
    {
        _broker = new FakeBroker();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_sut != null) await _sut.DisposeAsync().ConfigureAwait(false);
    }

    [DataRow(ShutdownInitiator.Peer, (ushort)404, (ushort)60, (ushort)40, true)]
    [DataRow(ShutdownInitiator.Peer, (ushort)403, (ushort)60, (ushort)40, true)]
    [DataRow(ShutdownInitiator.Peer, (ushort)406, (ushort)60, (ushort)40, true)]
    [DataRow(ShutdownInitiator.Library, (ushort)404, (ushort)60, (ushort)40, false)]
    [DataRow(ShutdownInitiator.Application, (ushort)404, (ushort)60, (ushort)40, false)]
    [DataRow(ShutdownInitiator.Peer, (ushort)404, (ushort)50, (ushort)10, false)]
    [DataRow(ShutdownInitiator.Peer, (ushort)320, (ushort)0, (ushort)0, false)]
    [TestMethod]
    public void IsPublishRefusal(ShutdownInitiator initiator, ushort replyCode, ushort classId, ushort methodId, bool expected)
    {
        var reason = new ShutdownEventArgs(initiator, replyCode, "text", classId, methodId);

        var refusal = PublishRecoveryChannel.IsPublishRefusal(reason);

        Assert.AreEqual(expected, refusal);
    }

    [TestMethod]
    public void IsPublishRefusal_Null_IsFalse()
    {
        var refusal = PublishRecoveryChannel.IsPublishRefusal(null);

        Assert.IsFalse(refusal);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task Enqueued_NextMessageIsPublishedOnlyAfterThePreviousIsConfirmed()
    {
        // Arrange
        var sut = CreateSut();
        var first = _broker.Message("first", Answer.None);
        var second = _broker.Message("second", Answer.None);

        // Act
        sut.Enqueue(first.Message);
        sut.Enqueue(second.Message);
        _broker.Ack(await _broker.WaitForPublishAsync(first).ConfigureAwait(false));
        _broker.Ack(await _broker.WaitForPublishAsync(second).ConfigureAwait(false));
        await second.Confirmed.ConfigureAwait(false);

        // Assert
        _broker.AssertLog("publish first", "ack first", "publish second", "ack second");
        Assert.AreEqual(1, _broker.CreateCalls);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task Nack_MessageIsRetriedAndConfirmedByTheRetry()
    {
        // Arrange
        var sut = CreateSut(retryInterval: Short);
        var message = _broker.Message("m", Answer.Nack, Answer.Ack);

        // Act
        sut.Enqueue(message.Message);
        await message.Confirmed.ConfigureAwait(false);

        // Assert
        _broker.AssertLog("publish m", "nack m", "publish m", "ack m");
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ConfirmTimeout_MessageIsRetriedOnANewChannel()
    {
        // Arrange
        var sut = CreateSut(retryInterval: Short);
        var message = _broker.Message("m", Answer.None, Answer.Ack);

        // Act
        sut.Enqueue(message.Message);
        await message.Confirmed.ConfigureAwait(false);

        // Assert
        _broker.AssertLog("publish m", "publish m", "ack m");
        var (timedOut, retried) = (_broker.Publishes[0], _broker.Publishes[1]);
        Assert.AreNotSame(timedOut.Channel, retried.Channel);
        Assert.IsTrue(timedOut.Channel.Disposed);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task Refusal_RefusedMessageIsRetriedOnAnotherChannelAndTheOthersAreConfirmed()
    {
        // Arrange
        var sut = CreateSut(retryLimit: 1_000, retryInterval: Short);
        var bad = _broker.Message("bad", Answer.Refuse);
        var good = _broker.Message("good");

        // Act
        sut.Enqueue(bad.Message);
        sut.Enqueue(good.Message);
        await good.Confirmed.ConfigureAwait(false);
        var retried = await _broker.WaitForPublishAsync(bad, occurrence: 2).ConfigureAwait(false);
        await sut.DisposeAsync().ConfigureAwait(false);

        // Assert
        Assert.AreNotSame(_broker.PublishesOf(good).Single().Channel, retried.Channel);
        Assert.IsFalse(bad.Confirmed.IsCompleted);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task RefusedOnEveryAttempt_FailsAfterRetryLimitPublishes()
    {
        // Arrange
        const uint retryLimit = 3;
        var sut = CreateSut(retryLimit, Short);
        var bad = _broker.Message("bad", Answer.Refuse);

        // Act
        sut.Enqueue(bad.Message);
        await Assert.ThrowsAsync<RetryLimitExceededException>(() => bad.Confirmed).ConfigureAwait(false);
        await sut.DisposeAsync().ConfigureAwait(false);

        // Assert
        Assert.AreEqual((int)retryLimit, _broker.PublishesOf(bad).Count);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task LateAckFromARetiredChannel_IsIgnored()
    {
        // Arrange
        var sut = CreateSut(retryInterval: Short);
        var message = _broker.Message("m", Answer.None, Answer.Nack, Answer.Ack);
        _broker.BeforeAnswer = published =>
        {
            if (_broker.PublishesOf(message).Count == 2) _broker.Ack(_broker.Publishes[0]);
        };

        // Act
        sut.Enqueue(message.Message);
        await message.Confirmed.ConfigureAwait(false);

        // Assert
        _broker.AssertLog("publish m", "publish m", "ack m", "nack m", "publish m", "ack m");
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ConnectionLost_MessageIsRetriedOnANewChannelOnceOneCanBeOpened()
    {
        // Arrange
        var sut = CreateSut(retryInterval: Short, networkRecoveryInterval: Short);
        var message = _broker.Message("m", Answer.LoseConnection, Answer.Ack);
        _broker.FailedOpensAfterConnectionLoss = 3;

        // Act
        sut.Enqueue(message.Message);
        await message.Confirmed.ConfigureAwait(false);

        // Assert
        _broker.AssertLog("publish m", "lose connection m", "publish m", "ack m");
        Assert.AreEqual(5, _broker.CreateCalls);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ChannelCannotBeOpened_NextAttemptWaitsForTheNetworkRecoveryInterval()
    {
        // Arrange
        var sut = CreateSut(networkRecoveryInterval: Long);
        var message = _broker.Message("m");
        _broker.FailCreates = 1;

        // Act
        sut.Enqueue(message.Message);
        await _broker.WaitUntilAsync(() => _broker.CreateCalls == 1).ConfigureAwait(false);
        await sut.DisposeAsync().ConfigureAwait(false);

        // Assert
        Assert.AreEqual(1, _broker.CreateCalls);
        _broker.AssertLog();
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ShutdownOfARetiredChannel_DoesNotFailTheAttemptOnTheNewChannel()
    {
        // Arrange
        var sut = CreateSut(retryInterval: Short);
        var message = _broker.Message("m", Answer.None, Answer.Ack);
        _broker.BeforeAnswer = published =>
        {
            if (_broker.PublishesOf(message).Count == 2) _broker.Publishes[0].Channel.Close(ConnectionLost);
        };

        // Act
        sut.Enqueue(message.Message);
        await message.Confirmed.ConfigureAwait(false);
        await sut.DisposeAsync().ConfigureAwait(false);

        // Assert
        _broker.AssertLog("publish m", "publish m", "ack m");
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task CancelledWhileQueued_IsNotPublished()
    {
        // Arrange
        var sut = CreateSut();
        using var cts = new CancellationTokenSource();
        var first = _broker.Message("first", Answer.None);
        var cancelled = _broker.Message("cancelled", cts.Token);
        var last = _broker.Message("last");

        // Act
        sut.Enqueue(first.Message);
        sut.Enqueue(cancelled.Message);
        sut.Enqueue(last.Message);
        var firstPublish = await _broker.WaitForPublishAsync(first).ConfigureAwait(false);
        cts.Cancel();
        _broker.Ack(firstPublish);
        await last.Confirmed.ConfigureAwait(false);

        // Assert
        _broker.AssertLog("publish first", "ack first", "publish last", "ack last");
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task UnexpectedException_DoesNotStopTheLoop()
    {
        // Arrange
        var sut = CreateSut(retryInterval: Short);
        var first = _broker.Message("first");
        var second = _broker.Message("second");
        _broker.ThrowOnNextPublish = new InvalidOperationException("boom");

        // Act
        sut.Enqueue(first.Message);
        sut.Enqueue(second.Message);

        // Assert
        await Task.WhenAll(first.Confirmed, second.Confirmed).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ChannelSetupFails_CreatedChannelIsDisposedAndSetupRetried()
    {
        // Arrange
        var setupCalls = 0;
        var sut = CreateSut(
            networkRecoveryInterval: Short,
            onChannelCreated: _ => Interlocked.Increment(ref setupCalls) == 1
                ? throw new InvalidOperationException("consume failed")
                : Task.CompletedTask);
        var message = _broker.Message("m");

        // Act
        sut.Enqueue(message.Message);
        await message.Confirmed.ConfigureAwait(false);

        // Assert
        Assert.AreEqual(2, setupCalls);
        Assert.IsTrue(_broker.Channels[0].Disposed);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task DisposeWhileWaitingForAConfirm_StopsTheLoopAndDisposesTheChannel()
    {
        // Arrange
        var sut = CreateSut();
        var message = _broker.Message("m", Answer.None);
        var late = _broker.Message("late");
        sut.Enqueue(message.Message);
        var published = await _broker.WaitForPublishAsync(message).ConfigureAwait(false);

        // Act
        await sut.DisposeAsync().ConfigureAwait(false);
        sut.Enqueue(late.Message);
        await sut.DisposeAsync().ConfigureAwait(false);

        // Assert
        Assert.IsTrue(published.Channel.Disposed);
        _broker.AssertLog("publish m");
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task WithinIdleTimeout_ChannelIsReused()
    {
        // Arrange
        var sut = CreateSut();
        var first = _broker.Message("first");
        var second = _broker.Message("second");

        // Act
        sut.Enqueue(first.Message);
        await first.Confirmed.ConfigureAwait(false);
        sut.Enqueue(second.Message);
        await second.Confirmed.ConfigureAwait(false);

        // Assert
        Assert.AreEqual(1, _broker.CreateCalls);
        Assert.IsFalse(_broker.Channels[0].Disposed);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task Idle_ChannelIsClosedAndReopenedOnDemand()
    {
        // Arrange
        var sut = CreateSut(idleTimeout: Short);
        var first = _broker.Message("first");
        var second = _broker.Message("second");

        // Act
        sut.Enqueue(first.Message);
        await _broker.WaitUntilAsync(() => _broker.Channels.Count == 1 && _broker.Channels[0].Disposed).ConfigureAwait(false);
        sut.Enqueue(second.Message);
        await second.Confirmed.ConfigureAwait(false);

        // Assert
        Assert.AreEqual(2, _broker.CreateCalls);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ConfirmedRequest_NextMessageIsPublishedOnlyAfterItsReply()
    {
        // Arrange
        var sut = CreateSut();
        var request = _broker.Message("request");
        var next = _broker.Message("next");
        var reply = ExpectReply(request);

        // Act
        sut.Enqueue(request.Message);
        sut.Enqueue(next.Message);
        await request.Confirmed.ConfigureAwait(false);
        reply.Arrive();
        await next.Confirmed.ConfigureAwait(false);

        // Assert
        _broker.AssertLog("publish request", "ack request", "reply request", "publish next", "ack next");
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ConfirmedRequest_ReplyNoLongerAwaited_NextMessageIsPublished()
    {
        // Arrange
        var sut = CreateSut();
        var request = _broker.Message("request");
        var next = _broker.Message("next");
        var reply = ExpectReply(request);

        // Act
        sut.Enqueue(request.Message);
        sut.Enqueue(next.Message);
        await request.Confirmed.ConfigureAwait(false);
        reply.Cancel();
        await next.Confirmed.ConfigureAwait(false);

        // Assert
        _broker.AssertLog("publish request", "ack request", "reply request cancelled", "publish next", "ack next");
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ConfirmedRequest_ReplyNeverArrives_NextMessageIsPublishedAfterTheIdleTimeout()
    {
        // Arrange
        var sut = CreateSut(idleTimeout: Short);
        var request = _broker.Message("request");
        var next = _broker.Message("next");
        ExpectReply(request);

        // Act
        sut.Enqueue(request.Message);
        sut.Enqueue(next.Message);
        await next.Confirmed.ConfigureAwait(false);

        // Assert
        _broker.AssertLog("publish request", "ack request", "publish next", "ack next");
        Assert.AreEqual(1, _broker.CreateCalls);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task DisposeWhileWaitingForAReply_NextMessageIsNotPublished()
    {
        // Arrange
        var sut = CreateSut();
        var request = _broker.Message("request");
        var next = _broker.Message("next");
        ExpectReply(request);
        sut.Enqueue(request.Message);
        sut.Enqueue(next.Message);
        await request.Confirmed.ConfigureAwait(false);

        // Act
        await sut.DisposeAsync().ConfigureAwait(false);

        // Assert
        _broker.AssertLog("publish request", "ack request");
        Assert.IsTrue(_broker.Channels[0].Disposed);
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task QuarantinedRequestConfirmed_RefusedRetriesWaitForItsReply()
    {
        // Arrange
        var sut = CreateSut(retryInterval: Short);
        var request = _broker.Message("request", Answer.Refuse, Answer.Refuse, Answer.Ack);
        var bad = _broker.Message("bad", Answer.Refuse);
        var reply = ExpectReply(request);

        // Act
        sut.Enqueue(request.Message);
        sut.Enqueue(bad.Message);
        await request.Confirmed.ConfigureAwait(false);
        reply.Arrive();
        await _broker.WaitUntilAsync(() => _broker.Happened("reply request", then: "publish bad")).ConfigureAwait(false);

        // Assert
        _broker.AssertNothingBetween("ack request", "reply request", "publish bad");
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task EnqueueRacingTheIdleShutdown_EveryMessageIsPublished()
    {
        // Arrange
        var sut = CreateSut(idleTimeout: TimeSpan.FromMilliseconds(1));
        var messages = Enumerable.Range(0, 200).Select(i => _broker.Message($"m{i}")).ToList();

        // Act
        for (var i = 0; i < messages.Count; i++)
        {
            sut.Enqueue(messages[i].Message);
            if (i % 10 == 9) await messages[i].Confirmed.ConfigureAwait(false);
        }

        // Assert
        await Task.WhenAll(messages.Select(m => m.Confirmed)).ConfigureAwait(false);
    }

    private PublishRecoveryChannel CreateSut(
        uint retryLimit = 100,
        TimeSpan? retryInterval = null,
        TimeSpan? idleTimeout = null,
        TimeSpan? networkRecoveryInterval = null,
        Func<IChannel, Task>? onChannelCreated = null)
    {
        _sut = new PublishRecoveryChannel(
            _broker.Connection,
            new CreateChannelOptions(true, false, consumerDispatchConcurrency: null),
            retryLimit,
            retryInterval ?? Long,
            networkRecoveryInterval ?? Short,
            idleTimeout ?? Long,
            onChannelCreated ?? (_ => Task.CompletedTask),
            header => _replies.TryGetValue(header.MessageId, out var reply) ? reply.Task : Task.CompletedTask,
            TestLoggerFactory.Instance.CreateLogger<PublishRecoveryChannelTests>());

        return _sut;
    }

    private Reply ExpectReply(TestMessage request)
    {
        var reply = new Reply(request.Name, _broker);
        _replies[request.Id] = reply;

        return reply;
    }

    /// <summary>
    /// The reply to a request; arriving or being cancelled is recorded in the broker log.
    /// </summary>
    private sealed class Reply
    {
        private readonly FakeBroker _broker;
        private readonly string _name;
        private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Reply(string name, FakeBroker broker)
        {
            _name = name;
            _broker = broker;
        }

        public Task Task => _tcs.Task;

        public void Arrive()
        {
            _broker.Record($"reply {_name}");
            _tcs.SetResult(true);
        }

        public void Cancel()
        {
            _broker.Record($"reply {_name} cancelled");
            _tcs.SetCanceled();
        }
    }

    private sealed class TestMessage
    {
        private readonly Queue<Answer> _answers;
        private Answer _last;

        public TestMessage(string name, CancellationToken token, Answer[] answers)
        {
            Name = name;
            _answers = new Queue<Answer>(answers);
            _last = Answer.Ack;

            var header = new CarrotHeader { Exchange = "exchange", RoutingKey = "key", MessageId = Guid.NewGuid() };
            var properties = new BasicProperties { MessageId = header.MessageId.ToString() };
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            token.Register(() => tcs.TrySetCanceled());
            Message = new PublisherConfirmMessage("payload", header, properties, tcs, token);
        }

        public string Name { get; }

        public Guid Id => Message.MessageHeader.MessageId;

        public PublisherConfirmMessage Message { get; }

        public Task Confirmed => Message.CompletionSource.Task;

        // The answers are given in order; the last one repeats, no answers means Ack.
        public Answer NextAnswer()
        {
            if (_answers.Count > 0) _last = _answers.Dequeue();

            return _last;
        }
    }

    /// <summary>
    /// A broker whose channels answer each publish synchronously as scripted per message and record every step in order.
    /// </summary>
    private sealed class FakeBroker
    {
        private readonly object _lock = new();
        private readonly List<string> _log = [];
        private readonly ConcurrentDictionary<Guid, TestMessage> _messages = new();
        private int _createCalls;

        public FakeBroker()
        {
            Connection = Substitute.For<IConnection>();
            Connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>(), Arg.Any<CancellationToken>())
                .Returns(_ => CreateChannel());
        }

        public IConnection Connection { get; }

        public List<FakeChannel> Channels { get; } = [];

        public List<Published> Publishes { get; } = [];

        public int CreateCalls => Volatile.Read(ref _createCalls);

        public int FailCreates { get; set; }

        public int FailedOpensAfterConnectionLoss { get; set; }

        public Exception? ThrowOnNextPublish { get; set; }

        public Action<Published>? BeforeAnswer { get; set; }

        public TestMessage Message(string name, params Answer[] answers)
        {
            return Message(name, CancellationToken.None, answers);
        }

        public TestMessage Message(string name, CancellationToken token, params Answer[] answers)
        {
            var message = new TestMessage(name, token, answers);
            _messages[message.Id] = message;

            return message;
        }

        public void Record(string entry)
        {
            lock (_lock)
            {
                _log.Add(entry);
            }
        }

        public void OnPublished(Published published)
        {
            lock (_lock)
            {
                Publishes.Add(published);
            }

            Record($"publish {published.Name}");
            BeforeAnswer?.Invoke(published);

            switch (_messages[published.MessageId].NextAnswer())
            {
                case Answer.Ack:
                    Ack(published);
                    break;
                case Answer.Nack:
                    Record($"nack {published.Name}");
                    published.Channel.RaiseNack(published.SeqNo);
                    break;
                case Answer.Refuse:
                    Record($"refuse {published.Name}");
                    published.Channel.Close(Refusal);
                    break;
                case Answer.LoseConnection:
                    Record($"lose connection {published.Name}");
                    FailCreates = FailedOpensAfterConnectionLoss;
                    published.Channel.Close(ConnectionLost);
                    break;
            }
        }

        public void Ack(Published published)
        {
            Record($"ack {published.Name}");
            published.Channel.RaiseAck(published.SeqNo);
        }

        public List<Published> PublishesOf(TestMessage message)
        {
            lock (_lock)
            {
                return Publishes.Where(p => p.MessageId == message.Id).ToList();
            }
        }

        public async Task<Published> WaitForPublishAsync(TestMessage message, int occurrence = 1)
        {
            await WaitUntilAsync(() => PublishesOf(message).Count >= occurrence).ConfigureAwait(false);

            return PublishesOf(message)[occurrence - 1];
        }

        public bool Happened(string first, string then)
        {
            var log = Snapshot();
            var index = log.IndexOf(first);

            return index >= 0 && log.Skip(index + 1).Contains(then, StringComparer.Ordinal);
        }

        public void AssertLog(params string[] expected)
        {
            CollectionAssert.AreEqual(expected, Snapshot(), string.Join(", ", Snapshot()));
        }

        public void AssertNothingBetween(string from, string to, string forbidden)
        {
            var log = Snapshot();
            var start = log.IndexOf(from);
            var end = log.IndexOf(to);
            Assert.IsTrue(start >= 0 && end > start, string.Join(", ", log));
            CollectionAssert.DoesNotContain(log.GetRange(start, end - start), forbidden, string.Join(", ", log));
        }

        public async Task WaitUntilAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(4);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("condition not met within 4 s");

                await Task.Delay(5).ConfigureAwait(false);
            }
        }

        private List<string> Snapshot()
        {
            lock (_lock)
            {
                return [.. _log];
            }
        }

        private Task<IChannel> CreateChannel()
        {
            Interlocked.Increment(ref _createCalls);
            if (FailCreates > 0)
            {
                FailCreates--;

                return Task.FromException<IChannel>(new AlreadyClosedException(ConnectionLost));
            }

            var channel = new FakeChannel(this);
            lock (_lock)
            {
                Channels.Add(channel);
            }

            return Task.FromResult(channel.Channel);
        }

        public string NameOf(Guid messageId)
        {
            return _messages.TryGetValue(messageId, out var message) ? message.Name : messageId.ToString();
        }
    }

    private sealed class FakeChannel
    {
        private long _seqNo;

        public FakeChannel(FakeBroker broker)
        {
            Channel = Substitute.For<IChannel>();
            Channel.IsOpen.Returns(_ => !Closed);
            Channel.GetNextPublishSequenceNumberAsync(Arg.Any<CancellationToken>()).Returns(_ => (ulong)Interlocked.Increment(ref _seqNo));
            Channel.When(async c => await c.BasicPublishAsync(
                        Arg.Any<string>(),
                        Arg.Any<string>(),
                        Arg.Any<bool>(),
                        Arg.Any<BasicProperties>(),
                        Arg.Any<ReadOnlyMemory<byte>>(),
                        Arg.Any<CancellationToken>())
                    .ConfigureAwait(false))
                .Do(ci =>
                {
                    if (broker.ThrowOnNextPublish is { } exception)
                    {
                        broker.ThrowOnNextPublish = null;

                        throw exception;
                    }

                    if (Closed) throw new AlreadyClosedException(CloseReason!);

                    var messageId = Guid.Parse(ci.ArgAt<BasicProperties>(3).MessageId!);
                    broker.OnPublished(new Published(this, messageId, broker.NameOf(messageId), (ulong)Interlocked.Read(ref _seqNo)));
                });
            Channel.When(async c => await c.DisposeAsync().ConfigureAwait(false)).Do(_ =>
            {
                Disposed = true;
                Closed = true;
            });
        }

        public IChannel Channel { get; }

        public bool Closed { get; private set; }

        public bool Disposed { get; private set; }

        public ShutdownEventArgs? CloseReason { get; private set; }

        public void Close(ShutdownEventArgs reason)
        {
            CloseReason = reason;
            Closed = true;
            Channel.ChannelShutdownAsync += Raise.Event<ShutdownAsyncEventHandler>(Channel, reason);
        }

        public void RaiseAck(ulong seqNo)
        {
            Channel.BasicAcksAsync += Raise.Event<AckAsyncEventHandler>(Channel, new BasicAckEventArgs(seqNo, false));
        }

        public void RaiseNack(ulong seqNo)
        {
            Channel.BasicNacksAsync += Raise.Event<NAckAsyncEventHandler>(Channel, new BasicNackEventArgs(seqNo, false, false));
        }
    }

    private sealed class Published
    {
        public Published(FakeChannel channel, Guid messageId, string name, ulong seqNo)
        {
            Channel = channel;
            MessageId = messageId;
            Name = name;
            SeqNo = seqNo;
        }

        public FakeChannel Channel { get; }

        public Guid MessageId { get; }

        public string Name { get; }

        public ulong SeqNo { get; }
    }
}