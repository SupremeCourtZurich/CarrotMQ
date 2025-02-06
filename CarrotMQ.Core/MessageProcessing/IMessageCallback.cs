using System;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core.MessageProcessing;

/// <summary>
/// Interface for handling CarrotMQ messages with callback functions.
/// </summary>
public interface IMessageCallback
{
    /// <summary>
    /// The type of the message that is handled by the callback
    /// </summary>
    public Type MessageType { get; }

    /// <summary>
    /// Handles the incoming CarrotMQ message.
    /// </summary>
    /// <param name="message">The CarrotMQ message to be processed.</param>
    /// <param name="consumerContext">The consumer context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task CallAsync(CarrotMessage message, ConsumerContext consumerContext, CancellationToken cancellationToken);
}