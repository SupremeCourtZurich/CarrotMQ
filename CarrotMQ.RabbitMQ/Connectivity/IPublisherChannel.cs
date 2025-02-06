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
    /// <param name="payload">The payload of the message to be published.</param>
    /// <param name="messageHeader">The header information for the message.</param>
    /// <param name="token">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation of publishing a message.</returns>
    Task PublishAsync(string payload, CarrotHeader messageHeader, CancellationToken token);
}