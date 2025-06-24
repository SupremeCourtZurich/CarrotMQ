using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Dto;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.EndPoints;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;

namespace CarrotMQ.Core.MessageSending;

internal class CarrotMessageBuilder : ICarrotMessageBuilder
{
    private readonly IEnumerable<IMessageEnricher> _messageEnrichers;
    private readonly IRoutingKeyResolver _routingKeyResolver;
    private readonly ICarrotSerializer _serializer;

    public CarrotMessageBuilder(
        IEnumerable<IMessageEnricher> messageEnrichers,
        ICarrotSerializer serializer,
        IRoutingKeyResolver routingKeyResolver)
    {
        _messageEnrichers = messageEnrichers;
        _serializer = serializer;
        _routingKeyResolver = routingKeyResolver;
    }

    public async Task<CarrotMessage> BuildCarrotMessageAsync<TEvent, TExchangeEndPoint>(
        IEvent<TEvent, TExchangeEndPoint> @event,
        Context? context,
        MessageProperties? messageProperties,
        CancellationToken cancellationToken) where TEvent : IEvent<TEvent, TExchangeEndPoint>
        where TExchangeEndPoint : ExchangeEndPoint, new()
    {
        var message = await BuildCarrotMessageInternalAsync(@event, new NoReplyEndPoint(), context, messageProperties, cancellationToken)
            .ConfigureAwait(false);

        return message;
    }

    public async Task<CarrotMessage> BuildCarrotMessageAsync<TEvent>(
        ICustomRoutingEvent<TEvent> @event,
        Context? context,
        MessageProperties? messageProperties,
        CancellationToken cancellationToken) where TEvent : ICustomRoutingEvent<TEvent>
    {
        var message = await BuildCarrotMessageInternalAsync(
                @event,
                @event.Exchange,
                @event.RoutingKey,
                new NoReplyEndPoint(),
                context,
                messageProperties,
                cancellationToken)
            .ConfigureAwait(false);

        return message;
    }

    public async Task<CarrotMessage> BuildCarrotMessageAsync<TCommand, TResponse, TEndPointDefinition>(
        ICommand<TCommand, TResponse, TEndPointDefinition> command,
        ReplyEndPointBase? replyEndPoint,
        Context? context,
        MessageProperties? messageProperties,
        Guid? correlationId,
        CancellationToken cancellationToken)
        where TResponse : class
        where TCommand : ICommand<TCommand, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        var message = await BuildCarrotMessageInternalAsync(
                command,
                replyEndPoint ?? new NoReplyEndPoint(),
                context,
                messageProperties,
                cancellationToken)
            .ConfigureAwait(false);
        message.Header.CorrelationId = correlationId;

        return message;
    }

    public async Task<CarrotMessage> BuildCarrotMessageAsync<TCommand, TResponse, TEndPointDefinition>(
        ICommand<TCommand, TResponse, TEndPointDefinition> request,
        Context? context,
        MessageProperties? messageProperties,
        CancellationToken cancellationToken) where TCommand : ICommand<TCommand, TResponse, TEndPointDefinition>
        where TResponse : class
        where TEndPointDefinition : EndPointBase, new()
    {
        return await BuildCarrotMessageInternalAsync(request, context, messageProperties, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CarrotMessage> BuildCarrotMessageAsync<TQuery, TResponse, TEndPointDefinition>(
        IQuery<TQuery, TResponse, TEndPointDefinition> query,
        ReplyEndPointBase replyEndPoint,
        Context? context,
        MessageProperties? messageProperties,
        Guid? correlationId,
        CancellationToken cancellationToken)
        where TResponse : class
        where TQuery : IQuery<TQuery, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        var message = await BuildCarrotMessageInternalAsync(query, replyEndPoint, context, messageProperties, cancellationToken)
            .ConfigureAwait(false);
        message.Header.CorrelationId = correlationId;

        return message;
    }

    public async Task<CarrotMessage> BuildCarrotMessageAsync<TQuery, TResponse, TEndPointDefinition>(
        IQuery<TQuery, TResponse, TEndPointDefinition> request,
        Context? context,
        MessageProperties? messageProperties,
        CancellationToken cancellationToken) where TQuery : IQuery<TQuery, TResponse, TEndPointDefinition>
        where TResponse : class
        where TEndPointDefinition : EndPointBase, new()
    {
        return await BuildCarrotMessageInternalAsync(request, context, messageProperties, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CarrotMessage> BuildCarrotMessageInternalAsync<TRequest, TResponse, TEndPointDefinition>(
        _IRequest<TRequest, TResponse, TEndPointDefinition> request,
        Context? context,
        MessageProperties? messageProperties,
        CancellationToken cancellationToken)
        where TResponse : class
        where TRequest : _IRequest<TRequest, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        var message = await BuildCarrotMessageInternalAsync(request, new DirectReplyEndPoint(), context, messageProperties, cancellationToken)
            .ConfigureAwait(false);
        message.Header.CorrelationId = Guid.NewGuid();

        return message;
    }

    private Task<CarrotMessage> BuildCarrotMessageInternalAsync<TRequest, TResponse, TEndPointDefinition>(
        _IMessage<TRequest, TResponse, TEndPointDefinition> request,
        ReplyEndPointBase replyEndPoint,
        Context? context,
        MessageProperties? messageProperties,
        CancellationToken cancellationToken)
        where TResponse : class
        where TRequest : _IMessage<TRequest, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        var requestEndPoint = new TEndPointDefinition();
        string exchange = requestEndPoint.Exchange;
        string routingKey = requestEndPoint.GetRoutingKey<TRequest>(_routingKeyResolver);

        return BuildCarrotMessageInternalAsync(request, exchange, routingKey, replyEndPoint, context, messageProperties, cancellationToken);
    }

    private async Task<CarrotMessage> BuildCarrotMessageInternalAsync<TRequest, TResponse>(
        _IMessage<TRequest, TResponse> request,
        string exchange,
        string routingKey,
        ReplyEndPointBase replyEndPoint,
        Context? context,
        MessageProperties? messageProperties,
        CancellationToken cancellationToken)
        where TResponse : class
        where TRequest : _IMessage<TRequest, TResponse>
    {
        Context ctx = context ?? new Context();

        foreach (IMessageEnricher enricher in _messageEnrichers)
        {
            await enricher.EnrichMessageAsync(request, ctx, cancellationToken).ConfigureAwait(false);
        }

        var header = new CarrotHeader
        {
            MessageId = Guid.NewGuid(),
            CalledMethod = request.GetType().FullName
                ?? throw new ArgumentException($"Can not get FullName of type {nameof(request)}", nameof(request)),
            Exchange = exchange,
            RoutingKey = routingKey,
            InitialUserName = ctx.InitialUserName,
            InitialServiceName = ctx.InitialServiceName,
            MessageProperties = messageProperties ?? MessageProperties.Default,
            CustomHeader = ctx.CustomHeader.ToDictionary(
                entry => entry.Key,
                entry => entry.Value),
            ReplyExchange = replyEndPoint.Exchange,
            ReplyRoutingKey = replyEndPoint.RoutingKey,
            IncludeRequestPayloadInResponse = replyEndPoint.IncludeRequestPayloadInResponse
        };

        string messagePayload = _serializer.Serialize(request);

        var message = new CarrotMessage(header, messagePayload);

        return message;
    }
}