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

namespace CarrotMQ.Core;

/// <summary>
/// Implementation of the <see cref="ICarrotClient" /> interface for publishing events and sending commands/queries.
/// When paired with our CarrotMQ.RabbitMQ NuGet package, this client enables the seamless transmission of messages over
/// RabbitMQ.
/// </summary>
public sealed class CarrotClient : ICarrotClient
{
    private readonly IEnumerable<IMessageEnricher> _messageEnrichers;
    private readonly IRoutingKeyResolver _routingKeyResolver;
    private readonly ICarrotSerializer _serializer;
    private readonly ITransport _transport;

    /// <summary>
    /// Initializes a new instance of the <see cref="CarrotClient" /> class.
    /// </summary>
    /// <param name="messageEnrichers"></param>
    /// <param name="transport">The transport mechanism for message exchange.</param>
    /// <param name="routingKeyResolver">The resolver for routing keys.</param>
    /// <param name="serializer">The serializer for message payloads.</param>
    public CarrotClient(
        IEnumerable<IMessageEnricher> messageEnrichers,
        ITransport transport,
        IRoutingKeyResolver routingKeyResolver,
        ICarrotSerializer serializer)
    {
        _messageEnrichers = messageEnrichers;
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _routingKeyResolver = routingKeyResolver ?? throw new ArgumentNullException(nameof(routingKeyResolver));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(
        ICustomRoutingEvent<TEvent> @event,
        Context? context = null,
        CancellationToken cancellationToken = default)
        where TEvent : ICustomRoutingEvent<TEvent>
    {
        var message = await BuildCarrotMessageAsync(@event, @event.Exchange, @event.RoutingKey, new NoReplyEndPoint(), context, cancellationToken)
            .ConfigureAwait(false);

        await SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent, TExchangeEndPoint>(
        IEvent<TEvent, TExchangeEndPoint> @event,
        Context? context = null,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent<TEvent, TExchangeEndPoint>
        where TExchangeEndPoint : ExchangeEndPoint, new()
    {
        var message = await BuildCarrotMessageAsync(@event, new NoReplyEndPoint(), context, cancellationToken).ConfigureAwait(false);

        await SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CarrotResponse<TCommand, TResponse>> SendReceiveAsync<TCommand, TResponse, TEndPointDefinition>(
        ICommand<TCommand, TResponse, TEndPointDefinition> command,
        Context? context = null,
        CancellationToken cancellationToken = default)
        where TResponse : class
        where TCommand : ICommand<TCommand, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        var message = await BuildSendReceiveMessageAsync(command, context, cancellationToken).ConfigureAwait(false);

        return await SendReceiveAsync<TCommand, TResponse, TEndPointDefinition>(message, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CarrotResponse<TQuery, TResponse>> SendReceiveAsync<TQuery, TResponse, TEndPointDefinition>(
        IQuery<TQuery, TResponse, TEndPointDefinition> query,
        Context? context = null,
        CancellationToken cancellationToken = default)
        where TResponse : class
        where TQuery : IQuery<TQuery, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        var message = await BuildSendReceiveMessageAsync(query, context, cancellationToken).ConfigureAwait(false);

        return await SendReceiveAsync<TQuery, TResponse, TEndPointDefinition>(message, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SendAsync<TCommand, TResponse, TEndPointDefinition>(
        ICommand<TCommand, TResponse, TEndPointDefinition> command,
        ReplyEndPointBase? replyEndPoint = null,
        Context? context = null,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default)
        where TResponse : class
        where TCommand : ICommand<TCommand, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        var message = await BuildCarrotMessageAsync(command, replyEndPoint ?? new NoReplyEndPoint(), context, cancellationToken)
            .ConfigureAwait(false);
        message.Header.CorrelationId = correlationId;

        await SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SendAsync<TQuery, TResponse, TEndPointDefinition>(
        IQuery<TQuery, TResponse, TEndPointDefinition> query,
        ReplyEndPointBase replyEndPoint,
        Context? context = null,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default)
        where TResponse : class
        where TQuery : IQuery<TQuery, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        var message = await BuildCarrotMessageAsync(query, replyEndPoint, context, cancellationToken).ConfigureAwait(false);
        message.Header.CorrelationId = correlationId;

        await SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CarrotMessage> BuildSendReceiveMessageAsync<TRequest, TResponse, TEndPointDefinition>(
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

    private async Task<CarrotMessage> BuildCarrotMessageAsync<TRequest, TResponse>(
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

    private async Task<CarrotResponse<TRequest, TResponse>> SendReceiveAsync<TRequest, TResponse, TEndPointDefinition>(
        CarrotMessage message,
        CancellationToken cancellationToken)
        where TResponse : class
        where TRequest : _IRequest<TRequest, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        CarrotMessage responseMessage;
        if (message.Header.MessageProperties.Ttl > 0)
        {
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedTokenSource.CancelAfter((int)message.Header.MessageProperties.Ttl);
            responseMessage = await _transport.SendReceiveAsync(message, linkedTokenSource.Token).ConfigureAwait(false);
        }
        else
        {
            responseMessage = await _transport.SendReceiveAsync(message, cancellationToken).ConfigureAwait(false);
        }

        CarrotResponse<TRequest, TResponse> response;
        try
        {
            response = _serializer.DeserializeWithNullCheck<CarrotResponse<TRequest, TResponse>>(responseMessage.Payload);
        }
        catch (Exception)
        {
            response = new CarrotResponse<TRequest, TResponse>(CarrotStatusCode.InternalServerError);
        }

        if (response.StatusCode == CarrotStatusCode.GatewayTimeout)
        {
            throw new OperationCanceledException("Operation was canceled.");
        }

        return response;
    }

    private async Task SendAsync(
        CarrotMessage message,
        CancellationToken cancellationToken)
    {
        if (message.Header.MessageProperties.Ttl > 0)
        {
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedTokenSource.CancelAfter((int)message.Header.MessageProperties.Ttl);
            await _transport.SendAsync(message, linkedTokenSource.Token).ConfigureAwait(false);
        }
        else
        {
            await _transport.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }
}