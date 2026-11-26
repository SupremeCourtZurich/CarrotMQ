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
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="token" /> is cancelled before the message was sent or, with publisher confirms,
    /// confirmed. The <see cref="Exception.InnerException" /> is a <see cref="PublishFailedException" /> whose
    /// <see cref="PublishFailedException.Reason" /> tells whether the broker was unreachable.
    /// </exception>
    /// <exception cref="PublishFailedException">
    /// Thrown when the channel was closed while the message was being sent, or when the retry limit was reached
    /// (<see cref="RetryLimitExceededException" />).
    /// </exception>
    Task PublishAsync(CarrotMessage message, CancellationToken token);
}