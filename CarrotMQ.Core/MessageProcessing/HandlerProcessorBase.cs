using System;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.MessageProcessing.Middleware;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core.MessageProcessing;

/// <summary>
/// Base class for handler processors responsible for instantiating the associated message handlers .
/// </summary>
/// <remarks>
/// The <see cref="HandlerProcessorBase" /> class provides common functionality for creating metadata about handlers and
/// handling messages.
/// </remarks>
public abstract class HandlerProcessorBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerProcessorBase" /> class.
    /// </summary>
    /// <param name="handlerKey">The unique key associated with the handler.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if the <paramref name="handlerKey" /> is null, empty, or consists only of
    /// white-space characters.
    /// </exception>
    protected HandlerProcessorBase(string handlerKey)
    {
        if (string.IsNullOrWhiteSpace(handlerKey))
        {
            throw new ArgumentException($"{nameof(handlerKey)} cannot be null or whitespace", nameof(handlerKey));
        }

        HandlerKey = handlerKey;
    }

    /// <summary>
    /// The type of the message that will be processed by the handler
    /// </summary>
    internal abstract Type MessageType { get; }

    /// <summary>
    /// The type of the handler that will process the message
    /// </summary>
    internal abstract Type HandlerType { get; }

    /// <summary>
    /// Unique key associated with the handler.
    /// </summary>
    internal string HandlerKey { get; }

    /// <summary>
    /// Creates the HandlerProcessor for a request handler based on specified generic parameters.
    /// </summary>
    /// <typeparam name="TRequestHandler">The type of the request handler.</typeparam>
    /// <typeparam name="TRequest">The type of the request message.</typeparam>
    /// <typeparam name="TResponse">The type of the response message.</typeparam>
    /// <returns>An instance of <see cref="HandlerProcessorBase" /> for request handlers.</returns>
    internal static HandlerProcessorBase CreateRequestHandlerProcessor<TRequestHandler, TRequest, TResponse>()
        where TRequestHandler : RequestHandlerBase<TRequest, TResponse>
        where TRequest : class, _IRequest<TRequest, TResponse>
        where TResponse : class
    {
        ValidateHandlerType<TRequestHandler, TRequest>();

        return new RequestHandlerProcessor<TRequestHandler, TRequest, TResponse>();
    }

    /// <summary>
    /// Creates the HandlerProcessor for a response handler based on specified generic parameters.
    /// </summary>
    /// <typeparam name="TResponseHandler">The type of the response handler.</typeparam>
    /// <typeparam name="TRequest">The type of the request message.</typeparam>
    /// <typeparam name="TResponse">The type of the response message.</typeparam>
    /// <returns>An instance of <see cref="HandlerProcessorBase" /> for response handlers.</returns>
    internal static HandlerProcessorBase CreateResponseHandlerProcessor<TResponseHandler, TRequest, TResponse>()
        where TResponseHandler : ResponseHandlerBase<TRequest, TResponse>
        where TRequest : class, _IRequest<TRequest, TResponse>
        where TResponse : class
    {
        ValidateHandlerType<TResponseHandler, TRequest>();

        return new ResponseHandlerProcessor<TResponseHandler, TRequest, TResponse>();
    }

    /// <summary>
    /// Creates the HandlerProcessor for an event handler based on specified generic parameters.
    /// </summary>
    /// <typeparam name="TEventHandler">The type of the event handler.</typeparam>
    /// <typeparam name="TEvent">The type of the event message.</typeparam>
    /// <returns>An instance of <see cref="HandlerProcessorBase" /> for event handlers.</returns>
    internal static HandlerProcessorBase CreateEventHandlerProcessor<TEventHandler, TEvent>()
        where TEventHandler : EventHandlerBase<TEvent>
        where TEvent : _IMessage<TEvent, NoResponse>
    {
        ValidateHandlerType<TEventHandler, TEvent>();

        return new EventHandlerProcessor<TEventHandler, TEvent>();
    }

    /// <summary>
    /// Validates that the specified handler type is not a generic type.
    /// </summary>
    /// <typeparam name="THandler">The type of the handler to validate.</typeparam>
    /// <typeparam name="TType">The type that should not be generic.</typeparam>
    /// <exception cref="GenericMessageTypeException">Thrown if the specified <typeparamref name="TType" /> is a generic type.</exception>
    private static void ValidateHandlerType<THandler, TType>()
    {
        var type = typeof(TType);
        if (type.IsGenericType)
        {
            throw GenericMessageTypeException.Create(type, typeof(THandler));
        }
    }

    /// <summary>
    /// Handles the message contained in the <see cref="MiddlewareContext" /> by instantiating the
    /// associated messageHandler and calling its <see cref="HandlerBase{TMessage,TResponse}.HandleAsync" /> method.
    /// </summary>
    /// <param name="middlewareContext">
    /// The <see cref="MiddlewareContext" /> containing the <see cref="CarrotMessage" />,
    /// the <see cref="ConsumerContext" /> and a <see cref="CancellationToken" />. Use the properties
    /// <see cref="MiddlewareContext.DeliveryStatus" /> and <see cref="MiddlewareContext.HandlerResult" />
    /// to set the handler result.
    /// </param>
    /// <param name="scopedDependencyInjector">The scoped dependency injector.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal abstract Task HandleAsync(MiddlewareContext middlewareContext, IDependencyInjector scopedDependencyInjector);

    /// <summary>
    /// Gets an instance of the specified handler type, using the scoped dependency injector.
    /// </summary>
    /// <typeparam name="THandler">The type of the handler to create.</typeparam>
    /// <typeparam name="TMessage">The type of the message the handler processes.</typeparam>
    /// <typeparam name="TResponse">The type of the response the handler generates.</typeparam>
    /// <param name="scopedDependencyInjector">The scoped dependency injector.</param>
    /// <returns>An instance of the specified handler type.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no handler of the specified type could be instantiated.</exception>
    protected static THandler GetHandler<THandler, TMessage, TResponse>(IDependencyInjector scopedDependencyInjector)
        where THandler : HandlerBase<TMessage, TResponse> where TResponse : class where TMessage : _IMessage<TMessage, TResponse>
    {
        var messageHandler = scopedDependencyInjector.CreateHandler<THandler, TMessage, TResponse>();

        if (messageHandler == null)
        {
            var messageHandlerType = typeof(THandler);

            throw new InvalidOperationException(
                $"No Handler of the type {messageHandlerType.Name} ({messageHandlerType.FullName}) could be instantiated");
        }

        return messageHandler;
    }
}