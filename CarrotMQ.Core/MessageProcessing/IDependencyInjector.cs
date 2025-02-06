using System;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.MessageProcessing.Middleware;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;

namespace CarrotMQ.Core.MessageProcessing;

/// <summary>
/// Interface used as abstraction between CarrotMQ and your DI framework.
/// It contains methods for the creation of all types, CarrotMQ.RabbitMQ needs to be able to instantiate dynamically.
/// </summary>
public interface IDependencyInjector : IAsyncDisposable
{
    /// <summary>
    /// Creates a new scope for dependency resolution.
    /// </summary>
    /// <returns>An instance of <see cref="IDependencyInjector" /> representing the new scope.</returns>
    /// <remarks>A new scope is created for each incoming message</remarks>
    IDependencyInjector CreateAsyncScope();

    /// <summary>
    /// Creates a handler for processing messages of type <typeparamref name="TMessage" /> with response type
    /// <typeparamref name="TResponse" />.
    /// </summary>
    /// <typeparam name="THandler">The type of the handler to be created.</typeparam>
    /// <typeparam name="TMessage">The type of the message to be handled.</typeparam>
    /// <typeparam name="TResponse">The type of the response produced by the handler.</typeparam>
    /// <returns>An instance of the handler specified by <typeparamref name="THandler" />.</returns>
    /// <remarks>
    /// All handlers that where registered must be able to
    /// be instantiated by this method (<see cref="EventHandlerBase{TEvent}" />,
    /// <see cref="CommandHandlerBase{TCommand,TResponse}" />, <see cref="QueryHandlerBase{TQuery,TResponse}" />,
    /// <see cref="ResponseHandlerBase{TRequest,TResponse}" />)
    /// </remarks>
    THandler? CreateHandler<THandler, TMessage, TResponse>() where THandler : HandlerBase<TMessage, TResponse>
        where TMessage : _IMessage<TMessage, TResponse>
        where TResponse : class;

    /// <summary>
    /// Gets the <see cref="ICarrotSerializer" /> that is used to serialize and deserialize the payload of a CarrotMQ message.
    /// </summary>
    ICarrotSerializer GetCarrotSerializer();

    /// <summary>
    /// Gets the <see cref="ITransport" /> that is used to send a response to a CarrotMQ message handled by
    /// <see cref="RequestHandlerBase{TRequest,TResponse}" />.
    /// </summary>
    ITransport GetTransport();

    /// <summary>
    /// Gets the <see cref="IMiddlewareProcessor" />.
    /// </summary>
    IMiddlewareProcessor GetMiddlewareProcessor();
}