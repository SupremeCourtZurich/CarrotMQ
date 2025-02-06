using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Handlers.HandlerResults;

namespace CarrotMQ.Core.Handlers;

/// <summary>
/// Represents a base class for message handlers.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
/// <typeparam name="TResponse">The type of the response produced by the handler.</typeparam>
/// <remarks>
/// You should never directly inherit from this class. Use <see cref="EventHandlerBase{TEvent}" />,
/// <see cref="QueryHandlerBase{TQuery,TResponse}" />, <see cref="ResponseHandlerBase{TRequest,TResponse}" /> or
/// <see cref="CommandHandlerBase{TCommand,TResponse}" /> instead
/// </remarks>
public abstract class HandlerBase<TMessage, TResponse>
    where TMessage : _IMessage<TMessage, TResponse>
    where TResponse : class
{
    /// <summary>
    /// Handles the incoming message and returns the result.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="consumerContext">The consumer context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public abstract Task<IHandlerResult> HandleAsync(
        TMessage message,
        ConsumerContext consumerContext,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a handler result indicating that the event processing should be retried.
    /// </summary>
    /// <remarks>It is recommended to delay before returning a retry.</remarks>
    public IHandlerResult Retry()
    {
        return new RetryResult();
    }

    /// <summary>
    /// Creates a handler result indicating that the event processing should be rejected.
    /// </summary>
    /// <remarks>If the queue is configured with a DeadLetter exchange, the message will be sent to that exchange</remarks>
    public IHandlerResult Reject()
    {
        return new RejectResult();
    }
}