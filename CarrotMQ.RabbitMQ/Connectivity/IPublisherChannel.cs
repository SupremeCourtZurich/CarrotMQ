using System;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// Represents a channel for publishing messages.
/// </summary>
public interface IPublisherChannel : ICarrotChannel
{
    /// <summary>
    /// Asynchronously publishes a message with the specified payload and header.
    /// </summary>
    /// <exception cref="PublishFailedException">
    /// Thrown when <paramref name="token" /> is cancelled before the message was sent or, with publisher confirms,
    /// confirmed (the <see cref="OperationCanceledException" /> is the inner exception), when the channel was closed while
    /// the message was being sent, or when the retry limit was reached (<see cref="RetryLimitExceededException" />).
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown when the channel is disposed before the publish completed.</exception>
    Task PublishAsync(CarrotMessage message, CancellationToken token);
}