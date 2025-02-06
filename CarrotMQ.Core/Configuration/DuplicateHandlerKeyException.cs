using System;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Handlers;

namespace CarrotMQ.Core.Configuration;

/// <summary>
/// Exception thrown when a duplicate handler key is detected during handler registration.
/// </summary>
public sealed class DuplicateHandlerKeyException : Exception
{
    /// <param name="message"></param>
    /// <remarks>Private constructor to enforce static factory methods usage.</remarks>
    private DuplicateHandlerKeyException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a <see cref="DuplicateHandlerKeyException" /> for a command handler registration issue.
    /// </summary>
    /// <typeparam name="TCommandHandler">Type of the command handler.</typeparam>
    /// <typeparam name="TCommand">Type of the command.</typeparam>
    /// <typeparam name="TResponse">Type of the response.</typeparam>
    /// <param name="handlerKey">The handler key causing the registration issue.</param>
    /// <returns>An instance of <see cref="DuplicateHandlerKeyException" />.</returns>
    public static DuplicateHandlerKeyException CreateCommandHandlerException<TCommandHandler, TCommand, TResponse>(string handlerKey)
        where TCommandHandler : CommandHandlerBase<TCommand, TResponse>
        where TCommand : _ICommand<TCommand, TResponse>
        where TResponse : class
    {
        var handlerType = typeof(TCommandHandler);
        var requestType = typeof(TCommand);

        return new DuplicateHandlerKeyException(GetErrorMessage("CommandHandler", handlerType, requestType, handlerKey));
    }

    /// <summary>
    /// Creates a <see cref="DuplicateHandlerKeyException" /> for a query handler registration issue.
    /// </summary>
    /// <typeparam name="TQueryHandler">Type of the query handler.</typeparam>
    /// <typeparam name="TQuery">Type of the query.</typeparam>
    /// <typeparam name="TResponse">Type of the response.</typeparam>
    /// <param name="handlerKey">The handler key causing the registration issue.</param>
    /// <returns>An instance of <see cref="DuplicateHandlerKeyException" />.</returns>
    public static DuplicateHandlerKeyException CreateQueryHandlerException<TQueryHandler, TQuery, TResponse>(string handlerKey)
        where TQueryHandler : QueryHandlerBase<TQuery, TResponse>
        where TQuery : _IQuery<TQuery, TResponse>
        where TResponse : class
    {
        var handlerType = typeof(TQueryHandler);
        var requestType = typeof(TQuery);

        return new DuplicateHandlerKeyException(GetErrorMessage("QueryHandler", handlerType, requestType, handlerKey));
    }

    /// <summary>
    /// Creates a <see cref="DuplicateHandlerKeyException" /> for a response handler registration issue.
    /// </summary>
    /// <typeparam name="TResponseHandler">Type of the response handler.</typeparam>
    /// <typeparam name="TRequest">Type of the request.</typeparam>
    /// <typeparam name="TResponse">Type of the response.</typeparam>
    /// <param name="handlerKey">The handler key causing the registration issue.</param>
    /// <returns>An instance of <see cref="DuplicateHandlerKeyException" />.</returns>
    public static DuplicateHandlerKeyException CreateResponseHandlerException<TResponseHandler, TRequest, TResponse>(string handlerKey)
        where TResponseHandler : ResponseHandlerBase<TRequest, TResponse>
        where TRequest : _IRequest<TRequest, TResponse>
        where TResponse : class
    {
        var handlerType = typeof(TResponseHandler);
        var requestType = typeof(TRequest);

        return new DuplicateHandlerKeyException(GetErrorMessage("ResponseHandler", handlerType, requestType, handlerKey));
    }

    /// <summary>
    /// Creates a <see cref="DuplicateHandlerKeyException" /> for an event handler registration issue.
    /// </summary>
    /// <typeparam name="TEventHandler">Type of the event handler.</typeparam>
    /// <typeparam name="TEvent">Type of the event.</typeparam>
    /// <param name="handlerKey">The handler key causing the registration issue.</param>
    /// <returns>An instance of <see cref="DuplicateHandlerKeyException" />.</returns>
    public static DuplicateHandlerKeyException CreateEventHandlerException<TEventHandler, TEvent>(string handlerKey)
        where TEventHandler : EventHandlerBase<TEvent>
        where TEvent : _IMessage<TEvent, NoResponse>
    {
        var handlerType = typeof(TEventHandler);
        var requestType = typeof(TEvent);

        return new DuplicateHandlerKeyException(GetErrorMessage("EventHandler", handlerType, requestType, handlerKey));
    }

    /// <summary>
    /// Generates an error message for a <see cref="DuplicateHandlerKeyException" />.
    /// </summary>
    /// <param name="handler">The type of the handler (e.g., CommandHandler, QueryHandler, ResponseHandler, EventHandler).</param>
    /// <param name="handlerType">The type of the handler class.</param>
    /// <param name="requestType">The type of the request associated with the handler.</param>
    /// <param name="handlerKey">The handler key causing the registration issue.</param>
    /// <returns>An error message for a <see cref="DuplicateHandlerKeyException" />.</returns>
    private static string GetErrorMessage(string handler, Type handlerType, Type requestType, string handlerKey)
    {
        return
            $"The {handler} of type {handlerType.Name} ({handlerType.FullName}) with the request type {requestType.Name} ({requestType.FullName}) could not be registered with the handlerKey {handlerKey}. If the handlerKey is empty, you might be trying to register a generic handler --> this is not allowed. If the handlerKey is not empty, you might already have defined another handler for this request type.";
    }
}