using System;
using System.Threading;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core.MessageProcessing.Middleware;

/// <summary>
/// Gives the <see cref="ICarrotMiddleware" /> the ability to control to read and change the
/// handler input and the response.
/// </summary>
public class MiddlewareContext
{
    /// <summary>
    /// Creates a new middleware context
    /// </summary>
    public MiddlewareContext(CarrotMessage message, Type messageType, ConsumerContext consumerContext, CancellationToken cancellationToken)
    {
        Message = message;
        MessageType = messageType;
        ConsumerContext = consumerContext;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// The message being processed
    /// </summary>
    public CarrotMessage Message { get; }

    /// <summary>
    /// The <see cref="ConsumerContext" /> of the current request
    /// </summary>
    public ConsumerContext ConsumerContext { get; }

    /// <summary>
    /// Provides cancellation support for the current processing pipeline
    /// </summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// The handler result, if the message is a request.
    /// </summary>
    public IHandlerResult? HandlerResult { get; set; }

    /// <summary>
    /// The <see cref="DeliveryStatus" />. Can be overwritten by the middleware
    /// to discard, ack or retry the message.
    /// </summary>
    public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.Reject;

    /// <summary>
    /// Signals whether a response is required to be sent.
    /// </summary>
    public bool ResponseRequired { get; set; }

    /// <summary>
    /// Signals whether a response has already been sent.
    /// </summary>
    /// <remarks>This flag can be used in <see cref="ICarrotMiddleware" /> if message sending is handled there.</remarks>
    public bool ResponseSent { get; set; }

    /// <summary>
    /// Signals whether the response contains an error description.
    /// If the handler or middleware throws an unhandled exception and this
    /// property is still <see langword="false" />, the framework will provide a default error response
    /// </summary>
    public bool IsErrorResult { get; set; }

    /// <summary>
    /// The type of the message handler, if one is registered
    /// </summary>
    public Type? HandlerType { get; internal set; }

    /// <summary>
    /// The type of the received message
    /// </summary>
    public Type MessageType { get; }
}