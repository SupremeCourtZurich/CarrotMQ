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
    Task SendAsync(CarrotMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a CarrotMQ message and asynchronously waits for a response.
    /// </summary>
    /// <param name="message">The CarrotMQ message to be sent.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The received CarrotMQ message as a response.</returns>
    Task<CarrotMessage> SendReceiveAsync(CarrotMessage message, CancellationToken cancellationToken);
}