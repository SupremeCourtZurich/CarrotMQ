using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// Represents a channel for direct reply communication, allowing asynchronous publishing with a reply.
/// </summary>
public interface IDirectReplyChannel : IPublisherChannel
{
    /// <summary>
    /// Publishes a message with a direct reply pattern asynchronously.
    /// </summary>
    /// <param name="messagePayload">The payload of the message.</param>
    /// <param name="carrotHeader">The header of the message.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation. The result is the reply message.</returns>
    Task<string> PublishWithReplyAsync(
        string messagePayload,
        CarrotHeader carrotHeader,
        CancellationToken token);
}