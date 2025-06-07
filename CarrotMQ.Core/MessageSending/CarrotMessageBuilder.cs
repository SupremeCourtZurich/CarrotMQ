
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

internal class CarrotMessageBuilder: ICarrotMessageBuilder
{
    private readonly IEnumerable<IMessageEnricher> _messageEnrichers;
    private readonly ICarrotSerializer _serializer;
    private readonly IRoutingKeyResolver _routingKeyResolver;

    public CarrotMessageBuilder(
        IEnumerable<IMessageEnricher> messageEnrichers,
        ICarrotSerializer serializer,
        IRoutingKeyResolver routingKeyResolver)
    {
        _messageEnrichers = messageEnrichers;
        _serializer = serializer;
        _routingKeyResolver = routingKeyResolver;
    }

    public async Task<CarrotMessage> BuildCarrotMessageAsync<TEvent, TExchangeEndPoint>(IEvent<TEvent, TExchangeEndPoint> @event, Context? context,
        CancellationToken cancellationToken) where TEvent : IEvent<TEvent, TExchangeEndPoint>
        where TExchangeEndPoint : ExchangeEndPoint, new()
    {
        var message = await BuildCarrotMessageAsync(@event, new NoReplyEndPoint(), context, cancellationToken).ConfigureAwait(false);
        return message;
    }

    public async Task<CarrotMessage> BuildCarrotMessageAsync<TCommand, TResponse, TEndPointDefinition>(ICommand<TCommand, TResponse, TEndPointDefinition> command,
        ReplyEndPointBase? replyEndPoint, Context? context, Guid? correlationId, CancellationToken cancellationToken)
        where TResponse : class
        where TCommand : ICommand<TCommand, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        var message = await BuildCarrotMessageAsync(command, replyEndPoint ?? new NoReplyEndPoint(), context, cancellationToken)
            .ConfigureAwait(false);
        message.Header.CorrelationId = correlationId;
        return message;
    }

    public async Task<CarrotMessage> BuildCarrotMessageAsync<TQuery, TResponse, TEndPointDefinition>(IQuery<TQuery, TResponse, TEndPointDefinition> query,
    ReplyEndPointBase replyEndPoint, Context? context, Guid? correlationId, CancellationToken cancellationToken)
    where TResponse : class
    where TQuery : IQuery<TQuery, TResponse, TEndPointDefinition>
    where TEndPointDefinition : EndPointBase, new()
    {
        var message = await BuildCarrotMessageAsync(query, replyEndPoint, context, cancellationToken).ConfigureAwait(false);
        message.Header.CorrelationId = correlationId;
        return message;
    }

    public async Task<CarrotMessage> BuildCarrotMessageAsync<TRequest, TResponse, TEndPointDefinition>(
        _IRequest<TRequest, TResponse, TEndPointDefinition> request,
        Context? context,
        CancellationToken cancellationToken)
        where TResponse : class
        where TRequest : _IRequest<TRequest, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        var message = await BuildCarrotMessageAsync(request, new DirectReplyEndPoint(), context, cancellationToken).ConfigureAwait(false);
        message.Header.CorrelationId = Guid.NewGuid();
        if (message.Header.MessageProperties.Ttl == null)
        {
            var messageProperties = message.Header.MessageProperties;
            messageProperties.Ttl = 5_000;
            message.Header.MessageProperties = messageProperties;
        }

        return message;
    }

    private Task<CarrotMessage> BuildCarrotMessageAsync<TRequest, TResponse, TEndPointDefinition>(
        _IMessage<TRequest, TResponse, TEndPointDefinition> request,
        ReplyEndPointBase replyEndPoint,
        Context? context,
        CancellationToken cancellationToken)
        where TResponse : class
        where TRequest : _IMessage<TRequest, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        var requestEndPoint = new TEndPointDefinition();
        var exchange = requestEndPoint.Exchange;
        var routingKey = requestEndPoint.GetRoutingKey<TRequest>(_routingKeyResolver);

        return BuildCarrotMessageAsync(request, exchange, routingKey, replyEndPoint, context, cancellationToken);
    }

    public async Task<CarrotMessage> BuildCarrotMessageAsync<TRequest, TResponse>(
        _IMessage<TRequest, TResponse> request,
        string exchange,
        string routingKey,
        ReplyEndPointBase replyEndPoint,
        Context? context,
        CancellationToken cancellationToken)
        where TResponse : class
        where TRequest : _IMessage<TRequest, TResponse>
    {
        var ctx = context ?? new Context();

        foreach (var enricher in _messageEnrichers)
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
            MessageProperties = ctx.MessageProperties,
            CustomHeader = ctx.CustomHeader.ToDictionary(
                entry => entry.Key,
                entry => entry.Value),
            ReplyExchange = replyEndPoint.Exchange,
            ReplyRoutingKey = replyEndPoint.RoutingKey,
            IncludeRequestPayloadInResponse = replyEndPoint.IncludeRequestPayloadInResponse
        };

        var messagePayload = _serializer.Serialize(request);

        var message = new CarrotMessage(header, messagePayload);

        return message;
    }
}