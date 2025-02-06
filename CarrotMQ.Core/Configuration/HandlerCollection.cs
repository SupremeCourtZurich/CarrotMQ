using System.Collections.Generic;
using CarrotMQ.Core.Dto;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.MessageProcessing;
using Microsoft.Extensions.DependencyInjection;

namespace CarrotMQ.Core.Configuration;

///
public class HandlerCollection
{
    private readonly BindingCollection _bindingCollection;
    private readonly Dictionary<string, HandlerProcessorBase> _registry = [];
    private readonly IServiceCollection _serviceCollection;

    ///
    public HandlerCollection(IServiceCollection serviceCollection, BindingCollection bindingCollection)
    {
        _serviceCollection = serviceCollection;
        _bindingCollection = bindingCollection;
    }

    /// <summary>
    /// Gets all registered handlers in the configuration.
    /// </summary>
    /// <returns>A dictionary of handler keys and associated handler processors.</returns>
    public IDictionary<string, HandlerProcessorBase> GetHandlers()
    {
        return _registry;
    }

    /// <summary>
    /// Registers a command handler in the service configuration.
    /// </summary>
    /// <typeparam name="TCommandHandler">The type of the command handler.</typeparam>
    /// <typeparam name="TCommand">The type of the command.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <remarks>
    /// This method registers the specified <typeparamref name="TCommandHandler" /> in the CarrotMQ messaging service
    /// configuration.
    /// </remarks>
    public Handler<TCommand, TResponse> AddCommand<TCommandHandler, TCommand, TResponse>()
        where TCommandHandler : CommandHandlerBase<TCommand, TResponse>
        where TCommand : class, _ICommand<TCommand, TResponse>
        where TResponse : class

    {
        var handlerCaller = HandlerProcessorBase.CreateRequestHandlerProcessor<TCommandHandler, TCommand, TResponse>();

        if (!_registry.TryAdd(handlerCaller.HandlerKey, handlerCaller))
        {
            throw DuplicateHandlerKeyException.CreateCommandHandlerException<TCommandHandler, TCommand, TResponse>(handlerCaller.HandlerKey);
        }

        _serviceCollection.AddTransient<TCommandHandler>();

        return new Handler<TCommand, TResponse>(_bindingCollection);
    }

    /// <summary>
    /// Registers a query handler in the service configuration.
    /// </summary>
    /// <typeparam name="TQueryHandler">The type of the query handler.</typeparam>
    /// <typeparam name="TQuery">The type of the query.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <remarks>
    /// This method registers the specified <typeparamref name="TQueryHandler" /> in the CarrotMQ messaging service
    /// configuration.
    /// </remarks>
    public Handler<TQuery, TResponse> AddQuery<TQueryHandler, TQuery, TResponse>()
        where TQueryHandler : QueryHandlerBase<TQuery, TResponse>
        where TQuery : class, _IQuery<TQuery, TResponse>
        where TResponse : class

    {
        var handlerCaller = HandlerProcessorBase.CreateRequestHandlerProcessor<TQueryHandler, TQuery, TResponse>();

        if (!_registry.TryAdd(handlerCaller.HandlerKey, handlerCaller))
        {
            throw DuplicateHandlerKeyException.CreateQueryHandlerException<TQueryHandler, TQuery, TResponse>(handlerCaller.HandlerKey);
        }

        _serviceCollection.AddTransient<TQueryHandler>();

        return new Handler<TQuery, TResponse>(_bindingCollection);
    }

    /// <summary>
    /// Registers a response handler in the service configuration.
    /// </summary>
    /// <typeparam name="TResponseHandler">The type of the response handler.</typeparam>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <remarks>
    /// This method registers the specified <typeparamref name="TResponseHandler" /> in the CarrotMQ messaging service
    /// configuration.
    /// </remarks>
    public void AddResponse<TResponseHandler, TRequest, TResponse>()
        where TResponseHandler : ResponseHandlerBase<TRequest, TResponse>
        where TRequest : class, _IRequest<TRequest, TResponse>
        where TResponse : class
    {
        var handlerCaller = HandlerProcessorBase.CreateResponseHandlerProcessor<TResponseHandler, TRequest, TResponse>();
        if (!_registry.TryAdd(handlerCaller.HandlerKey, handlerCaller))
        {
            throw DuplicateHandlerKeyException.CreateResponseHandlerException<TResponseHandler, TRequest, TResponse>(handlerCaller.HandlerKey);
        }

        _serviceCollection.AddTransient<TResponseHandler>();
    }

    /// <summary>
    /// Registers an event handler in service configuration.
    /// </summary>
    /// <typeparam name="TEventHandler">The type of the event handler.</typeparam>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <remarks>
    /// This method registers the specified <typeparamref name="TEventHandler" /> in the CarrotMQ messaging service
    /// configuration.
    /// </remarks>
    public Handler<TEvent, NoResponse> AddEvent<TEventHandler, TEvent>() where TEventHandler : EventHandlerBase<TEvent>
        where TEvent : class, _IEvent<TEvent>
    {
        AddEventInternal<TEventHandler, TEvent>();

        return new Handler<TEvent, NoResponse>(_bindingCollection);
    }

    /// <summary>
    /// Registers an event handler for a <see cref="ICustomRoutingEvent{TEvent}" /> in  service configuration.
    /// </summary>
    /// <typeparam name="TEventHandler">The type of the event handler.</typeparam>
    /// <typeparam name="TCustomRoutingEvent">The type of the event.</typeparam>
    /// <remarks>
    /// This method registers the specified <typeparamref name="TEventHandler" /> in the CarrotMQ messaging service
    /// configuration.
    /// </remarks>
    public void AddCustomRoutingEvent<TEventHandler, TCustomRoutingEvent>()
        where TEventHandler : EventHandlerBase<TCustomRoutingEvent>
        where TCustomRoutingEvent : class, ICustomRoutingEvent<TCustomRoutingEvent>
    {
        AddEventInternal<TEventHandler, TCustomRoutingEvent>();
    }

    private void AddEventInternal<TEventHandler, TEvent>() where TEventHandler : EventHandlerBase<TEvent>
        where TEvent : class, _IMessage<TEvent, NoResponse>
    {
        var handlerCaller = HandlerProcessorBase.CreateEventHandlerProcessor<TEventHandler, TEvent>();

        if (!_registry.TryAdd(handlerCaller.HandlerKey, handlerCaller))
        {
            throw DuplicateHandlerKeyException.CreateEventHandlerException<TEventHandler, TEvent>(handlerCaller.HandlerKey);
        }

        _serviceCollection.AddTransient<TEventHandler>();
    }

    /// <summary>
    /// Registers an event subscription for a <see cref="IEvent{TEvent,TExchangeEndPoint}" />.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <remarks>
    /// Registers a <see cref="EventSubscription{TEvent}" /> in the service collection which can be injected in your classes.
    /// You can then use <see cref="EventSubscription{TEvent}.EventReceived" /> to receive the events.
    /// </remarks>
    public Handler<TEvent, NoResponse> AddEventSubscription<TEvent>() where TEvent : class, _IEvent<TEvent>
    {
        _serviceCollection.AddSingleton<EventSubscription<TEvent>>();

        return AddEvent<SubscriptionEventHandler<TEvent>, TEvent>();
    }

    /// <summary>
    /// Registers an event subscription for a <see cref="ICustomRoutingEvent{TEvent}" />.
    /// </summary>
    /// <typeparam name="TCustomRoutingEvent">The type of the event.</typeparam>
    /// <remarks>
    /// Registers a <see cref="EventSubscription{TEvent}" /> in the service collection which can be injected in your classes.
    /// You can then use <see cref="EventSubscription{TEvent}.EventReceived" /> to receive the events.
    /// </remarks>
    public void AddCustomRoutingEventSubscription<TCustomRoutingEvent>()
        where TCustomRoutingEvent : class, ICustomRoutingEvent<TCustomRoutingEvent>
    {
        _serviceCollection.AddSingleton<EventSubscription<TCustomRoutingEvent>>();
        AddCustomRoutingEvent<SubscriptionEventHandler<TCustomRoutingEvent>, TCustomRoutingEvent>();
    }

    /// <summary>
    /// Registers a response subscription.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <remarks>
    /// Registers a <see cref="ResponseSubscription{TRequest,TResponse}" /> in the service collection which can be injected in your classes.
    /// You can then use <see cref="ResponseSubscription{TRequest,TResponse}" /> to receive the responses.
    /// </remarks>
    public void AddResponseSubscription<TRequest, TResponse>()
        where TRequest : class, _IRequest<TRequest, TResponse>
        where TResponse : class
    {
        _serviceCollection.AddSingleton<ResponseSubscription<TRequest, TResponse>>();
        AddResponse<SubscriptionResponseHandler<TRequest, TResponse>, TRequest, TResponse>();
    }
}