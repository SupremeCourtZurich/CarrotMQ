using System;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Dto;
using CarrotMQ.Core.EndPoints;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core.MessageSending;

/// <summary>
/// Creates a <see cref="CarrotMessage" /> from the strongly typed message.
/// It also invokes the <see cref="IMessageEnricher" />s.
/// The resulting messages are ready to be passed to the <see cref="ITransport" /> for sending.
/// </summary>
public interface ICarrotMessageBuilder
{
    /// <inheritdoc cref="ICarrotMessageBuilder" />
    public Task<CarrotMessage> BuildCarrotMessageAsync<TEvent, TExchangeEndPoint>(
        IEvent<TEvent, TExchangeEndPoint> @event,
        Context? context,
        MessageProperties? messageProperties,
        CancellationToken cancellationToken) where TEvent : IEvent<TEvent, TExchangeEndPoint>
        where TExchangeEndPoint : ExchangeEndPoint, new();

    /// <inheritdoc cref="ICarrotMessageBuilder" />
    public Task<CarrotMessage> BuildCarrotMessageAsync<TEvent>(
        ICustomRoutingEvent<TEvent> @event,
        Context? context,
        MessageProperties? messageProperties,
        CancellationToken cancellationToken) where TEvent : ICustomRoutingEvent<TEvent>;

    /// <inheritdoc cref="ICarrotMessageBuilder" />
    public Task<CarrotMessage> BuildCarrotMessageAsync<TCommand, TResponse, TEndPointDefinition>(
        ICommand<TCommand, TResponse, TEndPointDefinition> command,
        ReplyEndPointBase? replyEndPoint,
        Context? context,
        MessageProperties? messageProperties,
        Guid? correlationId,
        CancellationToken cancellationToken)
        where TResponse : class
        where TCommand : ICommand<TCommand, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new();

    /// <inheritdoc cref="ICarrotMessageBuilder" />
    public Task<CarrotMessage> BuildCarrotMessageAsync<TCommand, TResponse, TEndPointDefinition>(
        ICommand<TCommand, TResponse, TEndPointDefinition> request,
        Context? context,
        MessageProperties? messageProperties,
        CancellationToken cancellationToken)
        where TResponse : class
        where TCommand : ICommand<TCommand, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new();

    /// <inheritdoc cref="ICarrotMessageBuilder" />
    public Task<CarrotMessage> BuildCarrotMessageAsync<TQuery, TResponse, TEndPointDefinition>(
        IQuery<TQuery, TResponse, TEndPointDefinition> query,
        ReplyEndPointBase replyEndPoint,
        Context? context,
        MessageProperties? messageProperties,
        Guid? correlationId,
        CancellationToken cancellationToken)
        where TResponse : class
        where TQuery : IQuery<TQuery, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new();

    /// <inheritdoc cref="ICarrotMessageBuilder" />
    public Task<CarrotMessage> BuildCarrotMessageAsync<TQuery, TResponse, TEndPointDefinition>(
        IQuery<TQuery, TResponse, TEndPointDefinition> request,
        Context? context,
        MessageProperties? messageProperties,
        CancellationToken cancellationToken)
        where TResponse : class
        where TQuery : IQuery<TQuery, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new();
}