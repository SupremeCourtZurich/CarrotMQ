using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarrotMQ.Core.Protocol;

/// <summary>
/// Represents an interface for handling the transport of CarrotMQ messages.
/// </summary>
/// <remarks>
/// Our default <see cref="ITransport" /> implementation for RabbitMQ can be found in our CarrotMQ.RabbitMQ nuget
/// package.
/// </remarks>
public interface ITransport : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Sends a CarrotMQ message asynchronously.
    /// </summary>
    /// <param name="message">The CarrotMQ message to be sent.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken" /> is cancelled before the message was sent or, with publisher
    /// confirms, confirmed. The <see cref="Exception.InnerException" /> is a <see cref="PublishFailedException" /> whose
    /// <see cref="PublishFailedException.Reason" /> tells whether the broker was unreachable.
    /// </exception>
    /// <exception cref="PublishFailedException">
    /// Thrown when the message could not be published, for example when no connection was established within the initial
    /// connection timeout (<see cref="PublishFailureReason.Unreachable" />) or the retry limit was reached
    /// (<see cref="RetryLimitExceededException" />).
    /// </exception>
    Task SendAsync(CarrotMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a CarrotMQ message and asynchronously waits for a response.
    /// </summary>
    /// <param name="message">The CarrotMQ message to be sent.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The received CarrotMQ message as a response.</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken" /> is cancelled before the response arrived. When the message itself
    /// was not published, the <see cref="Exception.InnerException" /> is a <see cref="PublishFailedException" /> whose
    /// <see cref="PublishFailedException.Reason" /> tells whether the broker was unreachable.
    /// </exception>
    /// <exception cref="PublishFailedException">
    /// Thrown when the message could not be published, as for <see cref="SendAsync" />.
    /// </exception>
    Task<CarrotMessage> SendReceiveAsync(CarrotMessage message, CancellationToken cancellationToken);
}