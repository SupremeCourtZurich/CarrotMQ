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
    Task PublishAsync(CarrotMessage message, CancellationToken token);
}