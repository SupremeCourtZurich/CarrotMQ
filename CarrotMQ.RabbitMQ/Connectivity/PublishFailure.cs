using System;
using CarrotMQ.Core.Protocol;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// Maps AMQP channel closes to <see cref="PublishFailureReason" /> and builds the exceptions for failed publishes.
/// </summary>
internal static class PublishFailure
{
    /// <summary>
    /// The reason for a channel close, by protocol data only: initiator, reply code and the failed method.
    /// </summary>
    public static PublishFailureReason ReasonOf(ShutdownEventArgs close)
    {
        if (PublishRecoveryChannel.IsPublishRefusal(close))
        {
            return close.ReplyCode switch
            {
                404 => PublishFailureReason.DestinationNotFound,
                403 => PublishFailureReason.AccessRefused,
                406 => PublishFailureReason.MessageRejected,
                _ => PublishFailureReason.Unknown
            };
        }

        // 320 connection-forced and 541 internal-error close the connection, not just the channel.
        return close.Initiator == ShutdownInitiator.Library || close.ReplyCode is 320 or 541
            ? PublishFailureReason.Unreachable
            : PublishFailureReason.Unknown;
    }

    public static void RecordClose(PublisherConfirmMessage message, ShutdownEventArgs close)
    {
        // A refusal close belongs to the refused message, which only the recovery channel identifies.
        if (PublishRecoveryChannel.IsPublishRefusal(close))
        {
            RecordNoConfirm(message);

            return;
        }

        message.LastFailureReason = ReasonOf(close);
        message.LastFailure = new AlreadyClosedException(close);
    }

    public static void RecordNoConfirm(PublisherConfirmMessage message)
    {
        message.LastFailureReason = PublishFailureReason.Unknown;
        message.LastFailure = null;
    }

    public static RetryLimitExceededException Refused(PublisherConfirmMessage message, ShutdownEventArgs close)
    {
        var header = message.MessageHeader;

        return new RetryLimitExceededException(
            $"Publish to exchange '{header.Exchange}' with routing key '{header.RoutingKey}' was refused by the broker: {close.ReplyCode} {close.ReplyText}",
            ReasonOf(close),
            new AlreadyClosedException(close));
    }

    /// <summary>
    /// The reason for a publish whose token fired: <see cref="PublishFailureReason.Unreachable" /> while the connection is
    /// down or missing, otherwise <paramref name="lastFailure" />.
    /// </summary>
    public static PublishFailureReason ReasonAtCancellation(IConnection? connection, PublishFailureReason lastFailure)
    {
        return connection is { IsOpen: true } ? lastFailure : PublishFailureReason.Unreachable;
    }

    /// <summary>
    /// The reason for a publish whose token fired on a channel: <see cref="PublishFailureReason.Unreachable" /> while the
    /// connection is down or missing, or while the channel is closed and no other cause is known; otherwise
    /// <paramref name="lastFailure" />.
    /// </summary>
    public static PublishFailureReason ReasonAtCancellation(IConnection? connection, bool channelOpen, PublishFailureReason lastFailure)
    {
        var reason = ReasonAtCancellation(connection, lastFailure);

        return !channelOpen && reason == PublishFailureReason.Unknown ? PublishFailureReason.Unreachable : reason;
    }

    public static PublishFailedException Cancelled(CarrotHeader header, PublishFailureReason reason, OperationCanceledException cancellation)
    {
        return new PublishFailedException(
            $"Publish to exchange '{header.Exchange}' with routing key '{header.RoutingKey}' was cancelled before it completed ({reason}).",
            reason,
            cancellation);
    }

    /// <summary>
    /// A publish without confirm on a closed channel; a refusal close belongs to another message.
    /// </summary>
    public static PublishFailedException Closed(CarrotHeader header, IConnection connection, AlreadyClosedException cause)
    {
        var close = cause.ShutdownReason;
        var lastFailure = close == null || PublishRecoveryChannel.IsPublishRefusal(close) ? PublishFailureReason.Unknown : ReasonOf(close);
        var reason = ReasonAtCancellation(connection, lastFailure);

        return new PublishFailedException(
            $"Publish to exchange '{header.Exchange}' with routing key '{header.RoutingKey}' failed: the channel is closed ({reason}). {cause.Message}",
            reason,
            cause);
    }

    public static PublishFailedException Unreachable(CarrotHeader header, Exception cause)
    {
        return new PublishFailedException(
            $"Publish to exchange '{header.Exchange}' with routing key '{header.RoutingKey}' failed: no connection to the broker. {cause.Message}",
            PublishFailureReason.Unreachable,
            cause);
    }

    public static RetryLimitExceededException RetryLimitExceeded(PublisherConfirmMessage message, uint retryLimit)
    {
        var lastClose = (message.LastFailure as OperationInterruptedException)?.ShutdownReason;
        var text = lastClose == null
            ? $"No publisher confirm after {retryLimit} retries."
            : $"No publisher confirm after {retryLimit} retries. Last close: {lastClose.ReplyCode} {lastClose.ReplyText}";

        return new RetryLimitExceededException(text, message.LastFailureReason, message.LastFailure);
    }
}