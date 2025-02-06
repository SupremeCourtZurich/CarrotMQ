using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core.MessageProcessing;

/// <summary>
/// Represents a registry for processing CarrotMQ messages.
/// </summary>
/// <remarks>
/// The <see cref="IMessageDistributor" /> interface defines a contract for classes responsible for processing CarrotMQ
/// messages.
/// </remarks>
public interface IMessageDistributor
{
    /// <summary>
    /// Distributes the CarrotMQ message to all registered handlers and callback functions which are associated with the value of
    /// <see cref="CarrotHeader.CalledMethod" />.
    /// </summary>
    /// <param name="carrotMessage">The CarrotMQ message to be processed.</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns>A task representing the asynchronous operation and containing the result status of the message processing.</returns>
    Task<DeliveryStatus> DistributeAsync(CarrotMessage carrotMessage, CancellationToken cancellationToken);
}