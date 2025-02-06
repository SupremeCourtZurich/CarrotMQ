using CarrotMQ.Core.Common;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.MessageProcessing.Delivery;
using CarrotMQ.RabbitMQ.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class MultiAckDeliveryTest
{
    private readonly List<(ulong DeliveryTag, bool Multiple)> _ackedList = new();

    private readonly List<(ulong DeliveryTag, bool Requeue)> _rejectedList = new();
    private TestIntervalTimer _ackDeliveryIntervalTimer = null!;
    private IConsumerChannel _consumerChannel = null!;
    private IDateTimeProvider _dateTimeProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        _ackDeliveryIntervalTimer = new TestIntervalTimer();
        _consumerChannel = Substitute.For<IConsumerChannel>();
        _dateTimeProvider = Substitute.For<IDateTimeProvider>();
        _consumerChannel.When(c => c.AckAsync(Arg.Any<ulong>(), Arg.Any<bool>()))
            .Do(x => _ackedList.Add(new ValueTuple<ulong, bool>(x.ArgAt<ulong>(0), x.ArgAt<bool>(1))));

        _consumerChannel.When(c => c.RejectAsync(Arg.Any<ulong>(), Arg.Any<bool>()))
            .Do(x => _rejectedList.Add(new ValueTuple<ulong, bool>(x.ArgAt<ulong>(0), x.ArgAt<bool>(1))));
    }

    [TestMethod]
    public async Task Six_messages_multi_acked_3_by_3_Test()
    {
        var baseDateTime = DateTime.UtcNow;

        using var ackBox = new MultiAckDelivery(_consumerChannel, 3, _ackDeliveryIntervalTimer, _dateTimeProvider);

        // Message 1
        SetDateTimeTo(baseDateTime);
        await ackBox.DeliverAsync(1, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 2
        AdvanceDateTime(ref baseDateTime);
        await ackBox.DeliverAsync(2, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 3
        AdvanceDateTime(ref baseDateTime);
        await ackBox.DeliverAsync(3, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 4
        AdvanceDateTime(ref baseDateTime);
        await ackBox.DeliverAsync(4, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 5
        AdvanceDateTime(ref baseDateTime);
        await ackBox.DeliverAsync(5, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 6
        AdvanceDateTime(ref baseDateTime);
        await ackBox.DeliverAsync(6, DeliveryStatus.Ack).ConfigureAwait(false);

        Assert.AreEqual(2, _ackedList.Count);
        Assert.AreEqual(0, _rejectedList.Count);
        AssertAreEqual(3, _ackedList[0].DeliveryTag);
        AssertAreEqual(6, _ackedList[1].DeliveryTag);
        Assert.IsTrue(_ackedList[0].Multiple);
        Assert.IsTrue(_ackedList[1].Multiple);
    }

    [TestMethod]
    public async Task MultiAck_messages_single_acked_after_timeout()
    {
        var baseDateTime = DateTime.UtcNow;

        using var ackBox = new MultiAckDelivery(_consumerChannel, 3, _ackDeliveryIntervalTimer, _dateTimeProvider);

        // Message 1
        SetDateTimeTo(baseDateTime);
        await ackBox.DeliverAsync(1, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 2
        AdvanceDateTime(ref baseDateTime);
        await ackBox.DeliverAsync(2, DeliveryStatus.Ack).ConfigureAwait(false);

        // Simulate timeout
        AdvanceDateTime(ref baseDateTime, MultiAckDelivery.MaxMultiAckMessageAgeMs * 2);
        await FireAckDeliveryTimerEvent().ConfigureAwait(false);

        // Check both messages are single acked
        Assert.AreEqual(2, _ackedList.Count);
        AssertAreEqual(1, _ackedList[0].DeliveryTag);
        Assert.IsFalse(_ackedList[0].Multiple);
        AssertAreEqual(2, _ackedList[1].DeliveryTag);
        Assert.IsFalse(_ackedList[1].Multiple);
    }

    [TestMethod]
    [DataRow(1, 2, 3, 2)]
    [DataRow(1, 3, 2, 1)]
    [DataRow(3, 2, 1, 0)]
    public async Task MultiAck_messages_single_acked_after_nack(int deliveryTag1, int deliveryTag2, int deliveryTag3, int amountOfExpectedSingleAcks)
    {
        var baseDateTime = DateTime.UtcNow;

        using var ackBox = new MultiAckDelivery(_consumerChannel, 3, _ackDeliveryIntervalTimer, _dateTimeProvider);

        // Message 1
        SetDateTimeTo(baseDateTime);
        await ackBox.DeliverAsync((ulong)deliveryTag1, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 2
        AdvanceDateTime(ref baseDateTime);
        await ackBox.DeliverAsync((ulong)deliveryTag2, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 3 - NACK
        AdvanceDateTime(ref baseDateTime);
        await ackBox.DeliverAsync((ulong)deliveryTag3, DeliveryStatus.Reject).ConfigureAwait(false);

        // Check messages are single acked
        Assert.AreEqual(amountOfExpectedSingleAcks, _ackedList.Count);

        for (var i = 0; i < amountOfExpectedSingleAcks; i++)
        {
            Assert.IsFalse(_ackedList[i].Multiple);
        }

        // Check NACK has been rejected
        Assert.AreEqual(1, _rejectedList.Count);
        AssertAreEqual(deliveryTag3, _rejectedList[0].DeliveryTag);
    }

    [TestMethod]
    [DataRow(DeliveryStatus.Reject, false)]
    [DataRow(DeliveryStatus.Retry, true)]
    public async Task MultiAck_nack_message_requeue_flag_Test(DeliveryStatus nackDeliveryStatus, bool shouldRequeue)
    {
        var baseDateTime = DateTime.UtcNow;

        using var ackBox = new MultiAckDelivery(_consumerChannel, 3, _ackDeliveryIntervalTimer, _dateTimeProvider);

        // Message 1 - NACK
        AdvanceDateTime(ref baseDateTime);
        await ackBox.DeliverAsync(1, nackDeliveryStatus).ConfigureAwait(false);

        // Check NACK has been rejected
        Assert.AreEqual(1, _rejectedList.Count);
        AssertAreEqual(1, _rejectedList[0].DeliveryTag);
        Assert.AreEqual(shouldRequeue, _rejectedList[0].Requeue);
    }

    [TestMethod]
    public async Task MultiAck_continues_after_single_acks_due_to_timeout()
    {
        var baseDateTime = DateTime.UtcNow;

        using var ackBox = new MultiAckDelivery(_consumerChannel, 3, _ackDeliveryIntervalTimer, _dateTimeProvider);

        // Message 1
        SetDateTimeTo(baseDateTime);
        await ackBox.DeliverAsync(1, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 2
        AdvanceDateTime(ref baseDateTime);
        await ackBox.DeliverAsync(2, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 3 is not yet acked

        // Message 4
        AdvanceDateTime(ref baseDateTime);
        await ackBox.DeliverAsync(4, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 5
        AdvanceDateTime(ref baseDateTime);
        await ackBox.DeliverAsync(5, DeliveryStatus.Ack).ConfigureAwait(false);

        // Simulate multi ack timeout
        AdvanceDateTime(ref baseDateTime, MultiAckDelivery.MaxMultiAckMessageAgeMs * 2);
        await FireAckDeliveryTimerEvent().ConfigureAwait(false);

        // Message 6
        AdvanceDateTime(ref baseDateTime);
        await ackBox.DeliverAsync(6, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 7
        AdvanceDateTime(ref baseDateTime);
        await ackBox.DeliverAsync(7, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 3
        AdvanceDateTime(ref baseDateTime);
        await ackBox.DeliverAsync(3, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 8
        AdvanceDateTime(ref baseDateTime);
        await ackBox.DeliverAsync(8, DeliveryStatus.Ack).ConfigureAwait(false);

        // Check 4 first messages single acked after timeout
        AssertAreEqual(1, _ackedList[0].DeliveryTag);
        Assert.IsFalse(_ackedList[0].Multiple);
        AssertAreEqual(2, _ackedList[1].DeliveryTag);
        Assert.IsFalse(_ackedList[1].Multiple);
        AssertAreEqual(4, _ackedList[2].DeliveryTag);
        Assert.IsFalse(_ackedList[2].Multiple);
        AssertAreEqual(5, _ackedList[3].DeliveryTag);
        Assert.IsFalse(_ackedList[3].Multiple);

        // Check multiAck continues after message n°3 arrives
        AssertAreEqual(3, _ackedList[4].DeliveryTag);
        Assert.IsFalse(_ackedList[4].Multiple);

        // Check multiAck continues after message n°3 arrives
        AssertAreEqual(8, _ackedList[5].DeliveryTag);
        Assert.IsTrue(_ackedList[5].Multiple);
    }

    [TestMethod]
    public async Task MultiAck_single_nack_on_timeout()
    {
        var baseDateTime = DateTime.UtcNow;

        using var ackBox = new MultiAckDelivery(_consumerChannel, 3, _ackDeliveryIntervalTimer, _dateTimeProvider);

        // Message 1
        SetDateTimeTo(baseDateTime);
        await ackBox.DeliverAsync(1, DeliveryStatus.Reject).ConfigureAwait(false);

        // Simulate multi ack timeout
        AdvanceDateTime(ref baseDateTime, MultiAckDelivery.MaxMultiAckMessageAgeMs * 2);
        await FireAckDeliveryTimerEvent().ConfigureAwait(false);

        // Check 4 first messages single acked after timeout
        Assert.AreEqual(1, _rejectedList.Count);
        Assert.AreEqual(0, _ackedList.Count);
    }

    [TestMethod]
    public async Task MultiAck_followed_by_timeout()
    {
        var baseDateTime = DateTime.UtcNow;

        using var ackBox = new MultiAckDelivery(_consumerChannel, 3, _ackDeliveryIntervalTimer, _dateTimeProvider);

        // Message 1
        SetDateTimeTo(baseDateTime);
        await ackBox.DeliverAsync(1, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 2
        SetDateTimeTo(baseDateTime);
        await ackBox.DeliverAsync(2, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 3
        SetDateTimeTo(baseDateTime);
        await ackBox.DeliverAsync(3, DeliveryStatus.Ack).ConfigureAwait(false);

        // Simulate multi ack timeout
        AdvanceDateTime(ref baseDateTime, MultiAckDelivery.MaxMultiAckMessageAgeMs * 2);
        await FireAckDeliveryTimerEvent().ConfigureAwait(false);

        // Check 4 first messages single acked after timeout
        Assert.AreEqual(1, _ackedList.Count);
        AssertAreEqual(3, _ackedList[0].DeliveryTag);
        Assert.IsTrue(_ackedList[0].Multiple);
    }

    [TestMethod]
    public async Task MultiAck_followed_by_nack()
    {
        var baseDateTime = DateTime.UtcNow;

        using var ackBox = new MultiAckDelivery(_consumerChannel, 3, _ackDeliveryIntervalTimer, _dateTimeProvider);

        // Message 1
        SetDateTimeTo(baseDateTime);
        await ackBox.DeliverAsync(1, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 2
        SetDateTimeTo(baseDateTime);
        await ackBox.DeliverAsync(2, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 3
        SetDateTimeTo(baseDateTime);
        await ackBox.DeliverAsync(3, DeliveryStatus.Ack).ConfigureAwait(false);

        // Message 4 NACK
        SetDateTimeTo(baseDateTime);
        await ackBox.DeliverAsync(4, DeliveryStatus.Reject).ConfigureAwait(false);

        // Check 4 first messages single acked after timeout
        Assert.AreEqual(1, _ackedList.Count);
        AssertAreEqual(3, _ackedList[0].DeliveryTag);
        Assert.IsTrue(_ackedList[0].Multiple);
    }

    [TestMethod]
    public async Task MultiAck_nack_far_outside_of_multiAck_window()
    {
        var baseDateTime = DateTime.UtcNow;

        using var ackBox = new MultiAckDelivery(_consumerChannel, 3, _ackDeliveryIntervalTimer, _dateTimeProvider);

        await MultiAckTillDeliveryTagX(123, ackBox);
        _ackedList.Clear();

        SetDateTimeTo(baseDateTime);
        await ackBox.DeliverAsync(131, DeliveryStatus.Reject).ConfigureAwait(false);

        SetDateTimeTo(baseDateTime);
        await ackBox.DeliverAsync(130, DeliveryStatus.Ack).ConfigureAwait(false);

        // Check 130 is singleAcked immediately
        Assert.AreEqual(1, _ackedList.Count);
        AssertAreEqual(130, _ackedList[0].DeliveryTag);
        Assert.IsFalse(_ackedList[0].Multiple);
    }

    [TestMethod]
    public async Task MultiAck_single_ack_on_timeout()
    {
        var baseDateTime = DateTime.UtcNow;

        using var ackBox = new MultiAckDelivery(_consumerChannel, 3, _ackDeliveryIntervalTimer, _dateTimeProvider);

        // Message 1
        SetDateTimeTo(baseDateTime);
        await ackBox.DeliverAsync(1, DeliveryStatus.Ack).ConfigureAwait(false);

        // Simulate multi ack timeout
        AdvanceDateTime(ref baseDateTime, MultiAckDelivery.MaxMultiAckMessageAgeMs * 2);
        await FireAckDeliveryTimerEvent().ConfigureAwait(false);

        // Check 4 first messages single acked after timeout
        Assert.AreEqual(0, _rejectedList.Count);
        Assert.AreEqual(1, _ackedList.Count);
    }

    [TestMethod]
    public async Task MultiAck_parallel_ack()
    {
        const ushort multiAckWindowSize = 5;
        const ushort messageCount = 120;

        using var ackBox = new MultiAckDelivery(_consumerChannel, multiAckWindowSize, _ackDeliveryIntervalTimer, _dateTimeProvider);

        var startTime = DateTime.UtcNow;
        SetDateTimeTo(startTime);

        _ = Parallel.For(
            1,
            messageCount + 1,
            // ReSharper disable once AccessToDisposedClosure
            i => { ackBox.DeliverAsync((ulong)i, DeliveryStatus.Ack).GetAwaiter().GetResult(); });

        SetDateTimeTo(startTime.AddMilliseconds(MultiAckDelivery.MaxMultiAckMessageAgeMs + 100));

        await FireAckDeliveryTimerEvent().ConfigureAwait(false);

        var singleAcked = _ackedList.Where(a => a.Multiple == false).Select(a => a.DeliveryTag).ToList();
        var maxMultiAck = _ackedList.Where(a => a.Multiple).Max(a => a.DeliveryTag);

        Assert.AreEqual(
            0,
            _rejectedList.Count,
            $"There should not be any rejected messages in {nameof(_rejectedList)} -> {string.Join(",", _rejectedList.Select(s => $"{{ Requeue: {s.Requeue}, DeliveryTag: {s.DeliveryTag} }}\r\n"))}");

        Assert.IsTrue(
            singleAcked.Count < multiAckWindowSize,
            $"{singleAcked.Count} < {multiAckWindowSize} (the amount of single acked messages must be smaller than the multiack window size)");

        Assert.IsTrue(
            singleAcked.All(s => s > messageCount - multiAckWindowSize),
            $"All single acked messages should be bigger than {messageCount - multiAckWindowSize} -> {string.Join(",", singleAcked)}");

        Assert.IsTrue(
            maxMultiAck > messageCount - multiAckWindowSize,
            $"{maxMultiAck} >  {messageCount - multiAckWindowSize} (biggest multi ack should be inside the last multiack window)");

        Assert.AreEqual(messageCount, (ulong)singleAcked.Count + maxMultiAck, "Max(multi acked) + amount of single acked = messageCount");
    }

    private async Task FireAckDeliveryTimerEvent()
    {
        await _ackDeliveryIntervalTimer.FireTimedEvent(new IIntervalTimer.IntervalTimerElapsedEventArgs()).ConfigureAwait(false);
    }

    private void AdvanceDateTime(ref DateTime date, long advanceMs = 300)
    {
        date = date.AddMilliseconds(advanceMs);
        SetDateTimeTo(date);
    }

    private void SetDateTimeTo(DateTime date)
    {
        _dateTimeProvider.Now.Returns(date);
    }

    private void AssertAreEqual(int expected, ulong actual)
    {
        Assert.AreEqual((ulong)expected, actual);
    }

    private async Task MultiAckTillDeliveryTagX(ulong deliveryTagX, IAckDelivery ackDelivery)
    {
        for (ulong i = 0; i <= deliveryTagX; i++)
        {
            await ackDelivery.DeliverAsync(i, DeliveryStatus.Ack).ConfigureAwait(false);
        }
    }
}