using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.MessageProcessing.Delivery;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class SingleAckDeliveryTest
{
    private readonly List<(ulong DeliveryTag, bool Multiple)> _ackedList = new();
    private readonly List<(ulong DeliveryTag, bool Requeue)> _rejectedList = new();
    private IAckDelivery _ackBox = null!;
    private IConsumerChannel _consumerChannel = null!;

    [TestInitialize]
    public void Setup()
    {
        _consumerChannel = Substitute.For<IConsumerChannel>();

        _consumerChannel.When(c => c.AckAsync(Arg.Any<ulong>(), Arg.Any<bool>()))
            .Do(x => _ackedList.Add(new ValueTuple<ulong, bool>(x.Arg<ulong>(), x.ArgAt<bool>(1))));

        _consumerChannel.When(c => c.RejectAsync(Arg.Any<ulong>(), Arg.Any<bool>()))
            .Do(x => _rejectedList.Add(new ValueTuple<ulong, bool>(x.Arg<ulong>(), x.ArgAt<bool>(1))));

        _ackBox = new SingleAckDelivery(_consumerChannel);
    }

    [TestMethod]
    public async Task Single_ack_Test()
    {
        await _ackBox.DeliverAsync(1, DeliveryStatus.Ack).ConfigureAwait(false);
        await _ackBox.DeliverAsync(2, DeliveryStatus.Ack).ConfigureAwait(false);
        await _ackBox.DeliverAsync(3, DeliveryStatus.Ack).ConfigureAwait(false);

        Assert.AreEqual(3, _ackedList.Count);
        Assert.AreEqual(0, _rejectedList.Count);

        AssertAreEqual(1, _ackedList[0].DeliveryTag);
        AssertAreEqual(2, _ackedList[1].DeliveryTag);
        AssertAreEqual(3, _ackedList[2].DeliveryTag);

        Assert.IsFalse(_ackedList[0].Multiple);
        Assert.IsFalse(_ackedList[1].Multiple);
        Assert.IsFalse(_ackedList[2].Multiple);
    }

    [TestMethod]
    public async Task Single_reject_Test()
    {
        await _ackBox.DeliverAsync(1, DeliveryStatus.Reject).ConfigureAwait(false);
        await _ackBox.DeliverAsync(2, DeliveryStatus.Reject).ConfigureAwait(false);
        await _ackBox.DeliverAsync(3, DeliveryStatus.Reject).ConfigureAwait(false);

        Assert.AreEqual(0, _ackedList.Count);
        Assert.AreEqual(3, _rejectedList.Count);

        AssertAreEqual(1, _rejectedList[0].DeliveryTag);
        AssertAreEqual(2, _rejectedList[1].DeliveryTag);
        AssertAreEqual(3, _rejectedList[2].DeliveryTag);

        Assert.IsFalse(_rejectedList[0].Requeue);
        Assert.IsFalse(_rejectedList[1].Requeue);
        Assert.IsFalse(_rejectedList[2].Requeue);
    }

    [TestMethod]
    public async Task Single_retry_Test()
    {
        await _ackBox.DeliverAsync(1, DeliveryStatus.Retry).ConfigureAwait(false);
        await _ackBox.DeliverAsync(2, DeliveryStatus.Retry).ConfigureAwait(false);
        await _ackBox.DeliverAsync(3, DeliveryStatus.Retry).ConfigureAwait(false);

        Assert.AreEqual(0, _ackedList.Count);
        Assert.AreEqual(3, _rejectedList.Count);

        AssertAreEqual(1, _rejectedList[0].DeliveryTag);
        AssertAreEqual(2, _rejectedList[1].DeliveryTag);
        AssertAreEqual(3, _rejectedList[2].DeliveryTag);

        Assert.IsTrue(_rejectedList[0].Requeue);
        Assert.IsTrue(_rejectedList[1].Requeue);
        Assert.IsTrue(_rejectedList[2].Requeue);
    }

    [TestMethod]
    public async Task Single_ack_reject_retry_Test()
    {
        await _ackBox.DeliverAsync(1, DeliveryStatus.Ack).ConfigureAwait(false);
        await _ackBox.DeliverAsync(2, DeliveryStatus.Reject).ConfigureAwait(false);
        await _ackBox.DeliverAsync(3, DeliveryStatus.Retry).ConfigureAwait(false);

        Assert.AreEqual(1, _ackedList.Count);
        Assert.AreEqual(2, _rejectedList.Count);

        AssertAreEqual(1, _ackedList[0].DeliveryTag);
        AssertAreEqual(2, _rejectedList[0].DeliveryTag);
        AssertAreEqual(3, _rejectedList[1].DeliveryTag);

        Assert.IsFalse(_ackedList[0].Multiple);
        Assert.IsFalse(_rejectedList[0].Requeue);
        Assert.IsTrue(_rejectedList[1].Requeue);
    }

    [TestCleanup]
    public void CleanUp()
    {
        _ackBox.Dispose();
    }

    private void AssertAreEqual(int expected, ulong actual)
    {
        Assert.AreEqual((ulong)expected, actual);
    }
}