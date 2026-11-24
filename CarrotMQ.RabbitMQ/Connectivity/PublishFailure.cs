using System;
using System.Threading;
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
    /// The reason for a channel close: <see cref="PublishFailureReason.Unreachable" /> when the connection was lost,
    /// otherwise <see cref="PublishFailureReason.Unknown" />.
    /// </summary>
    public static PublishFailureReason ReasonOf(ShutdownEventArgs close)
    {
        // 320 connection-forced and 541 internal-error close the connection, not just the channel.
        return close.Initiator == ShutdownInitiator.Library || close.ReplyCode is 320 or 541
            ? PublishFailureReason.Unreachable
            : PublishFailureReason.Unknown;
    }

    public static void RecordClose(PublisherConfirmMessage message, ShutdownEventArgs close)
    {
        message.LastFailureReason = ReasonOf(close);
        message.LastFailure = new AlreadyClosedException(close);
    }

    public static void RecordNoConfirm(PublisherConfirmMessage message)
    {
        message.LastFailureReason = PublishFailureReason.Unknown;
        message.LastFailure = null;
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

    public static OperationCanceledException Cancelled(CarrotHeader header, PublishFailureReason reason, OperationCanceledException cancellation, CancellationToken token)
    {
        var message = $"Publish to exchange '{header.Exchange}' with routing key '{header.RoutingKey}' was cancelled before it completed ({reason}).";

        return new OperationCanceledException(message, new PublishFailedException(message, reason, cancellation), token);
    }

    /// <summary>
    /// A publish without confirm on a channel the broker closed while it was sent.
    /// </summary>
    public static PublishFailedException Closed(CarrotHeader header, IConnection connection, AlreadyClosedException cause)
    {
        var lastFailure = cause.ShutdownReason == null ? PublishFailureReason.Unknown : ReasonOf(cause.ShutdownReason);
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
