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
    Task<CarrotMessage> PublishWithReplyAsync(CarrotMessage message, CancellationToken token);
}