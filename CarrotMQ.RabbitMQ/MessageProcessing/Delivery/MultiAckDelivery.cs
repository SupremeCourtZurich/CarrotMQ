using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CarrotMQ.Core.Common;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.RabbitMQ.Connectivity;

namespace CarrotMQ.RabbitMQ.MessageProcessing.Delivery;

/// <summary>
/// Represents a delivery mechanism that supports multiple acknowledgments for message processing (multiple messages are
/// acked with one call to the broker).
/// </summary>
internal sealed class MultiAckDelivery : IAckDelivery
{
    /// <summary>
    /// The maximum age (in milliseconds) of messages in the multi-ack delivery list.
    /// </summary>
    public const long MaxMultiAckMessageAgeMs = 1000;
    /// <summary>
    /// The interval (in milliseconds) for the single acknowledgment timer.
    /// All <see cref="SingleAckTimerIntervalMs" /> ms the messages older than <see cref="MaxMultiAckMessageAgeMs" /> will be
    /// single acked
    /// </summary>
    public const int SingleAckTimerIntervalMs = 2000;
    private readonly IConsumerChannel _channel;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly HashSet<ulong> _deliveryTagsNotIncludedInBiggestAckableDeliveryTag = new();
    private readonly AsyncLock _lock = new();
    private readonly Dictionary<ulong, DeliveryStatus> _multiAckDeliveryList = new();
    private readonly ushort _multipleCount;
    private readonly IIntervalTimer _singleAckTimer;
    private ulong _biggestAckableDeliveryTag;
    private bool _disposed;
    private DateTimeOffset _lastMultiAckExecutedTime = DateTimeOffset.MinValue;
    private ulong _multiAckWindowStart;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiAckDelivery" /> class.
    /// </summary>
    /// <param name="channel">The consumer channel.</param>
    /// <param name="multipleCount">The number of acknowledgments to wait for before acknowledging multiple messages together.</param>
    /// <param name="intervalTimer">The interval timer for single acknowledgments.</param>
    /// <param name="dateTimeProvider">The provider for the current date and time.</param>
    public MultiAckDelivery(
        IConsumerChannel channel,
        ushort multipleCount,
        IIntervalTimer? intervalTimer = null,
        IDateTimeProvider? dateTimeProvider = null)
    {
        if (multipleCount < 2) throw new ArgumentException("multipleCount must be at least 2!", nameof(multipleCount));

        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _multipleCount = multipleCount;
        _singleAckTimer = intervalTimer ?? new IntervalTimer(SingleAckTimerIntervalMs);
        _dateTimeProvider = dateTimeProvider ?? new DateTimeProvider();

        _singleAckTimer.ElapsedAsync += async (_, _) => await ProcessOldMessagesWithSingleAckAsync().ConfigureAwait(false);
        _singleAckTimer.Start();
    }

    /// <summary>
    /// Delivers the acknowledgment status for a message with the specified delivery tag.
    /// </summary>
    /// <param name="deliveryTag">The unique identifier associated with the delivered message.</param>
    /// <param name="deliveryStatus">The status indicating the result of the message processing.</param>
    public async Task DeliverAsync(ulong deliveryTag, DeliveryStatus deliveryStatus)
    {
        if (_disposed)
        {
            return;
        }

        using var scope = await _lock.LockAsync().ConfigureAwait(false);
        CalculateBiggestAckableDeliveryTag(deliveryTag);

        if (deliveryStatus == DeliveryStatus.Ack)
        {
            await DeliverAckAsync(deliveryTag, deliveryStatus).ConfigureAwait(false);
        }
        else
        {
            await DeliverNAckAsync(deliveryTag, deliveryStatus).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles the delivery acknowledgment for a message with the specified delivery tag and delivery status.
    /// </summary>
    /// <remarks>
    /// If the current <paramref name="deliveryTag" /> is not in the current multiAckWindow (a nack with a bigger delivery Tag
    /// has already been received), then this message will be single acked immediately.<br />
    /// Else the <paramref name="deliveryTag" /> is added to the <see cref="_multiAckDeliveryList" />
    /// </remarks>
    /// <param name="deliveryTag">The unique identifier associated with the delivered message.</param>
    /// <param name="deliveryStatus">The status indicating the result of the message processing.</param>
    private async Task DeliverAckAsync(ulong deliveryTag, DeliveryStatus deliveryStatus)
    {
        var notInMultiAckWindow = deliveryTag <= _multiAckWindowStart;

        if (notInMultiAckWindow)
        {
            await SingleAckAsync(deliveryTag).ConfigureAwait(false);
        }
        else
        {
            AddToMultiAckDeliveryList(deliveryTag, deliveryStatus);
            await DeliverMultiAckAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Nack this message and single-ack already received messages with deliveryTag &lt; <paramref name="deliveryTag" />  and
    /// reset ack window.
    /// </summary>
    /// <remarks>
    /// The AMQP Protocol guarantees that "methods are received in the same order as they are sent" (see AMQP  0.9.1 Chapter
    /// 4.7 Content Ordering Guarantees)
    /// <br />
    /// We can therefore assume that our reject is handled by the broker before the next multi-ack
    /// </remarks>
    private async Task DeliverNAckAsync(ulong deliveryTag, DeliveryStatus deliveryStatus)
    {
        if (deliveryTag > _multiAckWindowStart)
        {
            _multiAckWindowStart = deliveryTag;
            await SingleAckAllBellowMultiAckWindowAsync().ConfigureAwait(false);
        }

        if (deliveryStatus == DeliveryStatus.Retry)
        {
            await RetryAsync(deliveryTag).ConfigureAwait(false);
        }
        else if (deliveryStatus == DeliveryStatus.Reject)
        {
            await RejectAsync(deliveryTag).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Processes old messages with single acknowledgment if the oldest message is to old (>
    /// <see cref="MaxMultiAckMessageAgeMs" />)
    /// </summary>
    private async Task ProcessOldMessagesWithSingleAckAsync()
    {
        using var scope = await _lock.LockAsync().ConfigureAwait(false);

        var hasItemsToAck = _multiAckDeliveryList.Count > 0;
        var isOldestMessageToOld = GetOldestUnAckedMessageAge() > MaxMultiAckMessageAgeMs;

        if (hasItemsToAck && isOldestMessageToOld)
        {
            _multiAckWindowStart = _multiAckDeliveryList.Max(d => d.Key);
            await SingleAckAllBellowMultiAckWindowAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Adds a delivery tag and its associated delivery status to the multi-ack delivery list.
    /// If the list is empty, updates the last multi-ack executed time.
    /// </summary>
    /// <param name="deliveryTag">The delivery tag to add.</param>
    /// <param name="deliveryStatus">The delivery status associated with the delivery tag.</param>
    private void AddToMultiAckDeliveryList(ulong deliveryTag, DeliveryStatus deliveryStatus)
    {
        if (_multiAckDeliveryList.Count == 0)
        {
            _lastMultiAckExecutedTime = _dateTimeProvider.Now;
        }

        _multiAckDeliveryList.Add(deliveryTag, deliveryStatus);
    }

    /// <summary>
    /// Calculates the biggest ackable delivery tag.
    /// </summary>
    /// <param name="deliveryTag">The new delivery tag</param>
    private void CalculateBiggestAckableDeliveryTag(ulong deliveryTag)
    {
        _deliveryTagsNotIncludedInBiggestAckableDeliveryTag.Add(deliveryTag);
        while (true)
        {
            var nextBiggestDeliveryTag = _biggestAckableDeliveryTag + 1;
            if (_deliveryTagsNotIncludedInBiggestAckableDeliveryTag.Contains(nextBiggestDeliveryTag))
            {
                _biggestAckableDeliveryTag = nextBiggestDeliveryTag;
                _deliveryTagsNotIncludedInBiggestAckableDeliveryTag.Remove(nextBiggestDeliveryTag);
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Single-ack all delivery tags that are bellow the start of the current multi-ack window
    /// </summary>
    private async Task SingleAckAllBellowMultiAckWindowAsync()
    {
        var tagsToDeliver = _multiAckDeliveryList.Where(d => d.Key <= _multiAckWindowStart)
            .Select(d => d.Key)
            .ToList();
        foreach (var tag in tagsToDeliver)
        {
            await SingleAckAsync(tag).ConfigureAwait(false);
            _multiAckDeliveryList.Remove(tag);
        }
    }

    /// <summary>
    /// The message is rejected with the option to requeue (retry).
    /// </summary>
    /// <param name="deliveryTag">The delivery tag of the message to be retried.</param>
    private async Task RetryAsync(ulong deliveryTag)
    {
        await _channel.RejectAsync(deliveryTag, true).ConfigureAwait(false);
    }

    /// <summary>
    /// The message is rejected (if a deadLetter exchange is set on the queue, the message will be sent to that exchange)
    /// </summary>
    /// <param name="deliveryTag">The delivery tag of the message to be rejected.</param>
    private async Task RejectAsync(ulong deliveryTag)
    {
        await _channel.RejectAsync(deliveryTag, false).ConfigureAwait(false);
    }

    /// <summary>
    /// The message is acked
    /// </summary>
    /// <param name="deliveryTag">The delivery tag of the message to be acked.</param>
    private async Task SingleAckAsync(ulong deliveryTag)
    {
        await _channel.AckAsync(deliveryTag, false).ConfigureAwait(false);
    }

    /// <summary>
    /// Send a multi-ack to the broker if enough continuous delivery tags are ready to be acked
    /// </summary>
    private async Task DeliverMultiAckAsync()
    {
        if (_biggestAckableDeliveryTag < _multiAckWindowStart + _multipleCount)
        {
            return;
        }

        for (var i = _multiAckWindowStart + 1; i <= _biggestAckableDeliveryTag; i++)
        {
            _multiAckDeliveryList.Remove(i);
        }

        await _channel.AckAsync(_biggestAckableDeliveryTag, true).ConfigureAwait(false); // Multi AckAsync all previous messages

        _lastMultiAckExecutedTime = _dateTimeProvider.Now;

        _multiAckWindowStart = _biggestAckableDeliveryTag;
    }

    /// <summary>
    /// Get the age of oldest un-acked message
    /// </summary>
    /// <returns>The age of oldest un-acked message in millisends</returns>
    private double GetOldestUnAckedMessageAge()
    {
        return (_dateTimeProvider.Now - _lastMultiAckExecutedTime).TotalMilliseconds;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _singleAckTimer.Dispose();
    }
}