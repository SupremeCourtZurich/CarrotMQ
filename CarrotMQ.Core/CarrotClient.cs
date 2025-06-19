using System;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Dto;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.EndPoints;
using CarrotMQ.Core.MessageSending;
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
    private readonly ICarrotMessageBuilder _messageBuilder;
    private readonly ICarrotSerializer _serializer;
    private readonly ITransport _transport;

    /// <summary>
    /// Initializes a new instance of the <see cref="CarrotClient" /> class.
    /// </summary>
    /// <param name="transport">The transport mechanism for message exchange.</param>
    /// <param name="serializer">The serializer for message payloads.</param>
    /// <param name="messageBuilder">The message builder used to convert typed messages into <see cref="CarrotMessage" />s</param>
    public CarrotClient(
        ITransport transport,
        ICarrotSerializer serializer,
        ICarrotMessageBuilder messageBuilder)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _messageBuilder = messageBuilder ?? throw new ArgumentNullException(nameof(messageBuilder));
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(
        ICustomRoutingEvent<TEvent> @event,
        Context? context = null,
        MessageProperties? messageProperties = null,
        CancellationToken cancellationToken = default)
        where TEvent : ICustomRoutingEvent<TEvent>
    {
        CarrotMessage message = await _messageBuilder
            .BuildCarrotMessageAsync(@event, context, messageProperties, cancellationToken)
            .ConfigureAwait(false);

        await SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent, TExchangeEndPoint>(
        IEvent<TEvent, TExchangeEndPoint> @event,
        Context? context = null,
        MessageProperties? messageProperties = null,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent<TEvent, TExchangeEndPoint>
        where TExchangeEndPoint : ExchangeEndPoint, new()
    {
        CarrotMessage message = await _messageBuilder
            .BuildCarrotMessageAsync(@event, context, messageProperties, cancellationToken)
            .ConfigureAwait(false);

        await SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CarrotResponse<TCommand, TResponse>> SendReceiveAsync<TCommand, TResponse, TEndPointDefinition>(
        ICommand<TCommand, TResponse, TEndPointDefinition> command,
        Context? context = null,
        MessageProperties? messageProperties = null,
        CancellationToken cancellationToken = default)
        where TResponse : class
        where TCommand : ICommand<TCommand, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        CarrotMessage message = await _messageBuilder
            .BuildCarrotMessageAsync(command, context, messageProperties, cancellationToken)
            .ConfigureAwait(false);

        return await SendReceiveAsync<TCommand, TResponse, TEndPointDefinition>(message, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CarrotResponse<TQuery, TResponse>> SendReceiveAsync<TQuery, TResponse, TEndPointDefinition>(
        IQuery<TQuery, TResponse, TEndPointDefinition> query,
        Context? context = null,
        MessageProperties? messageProperties = null,
        CancellationToken cancellationToken = default)
        where TResponse : class
        where TQuery : IQuery<TQuery, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        CarrotMessage message = await _messageBuilder
            .BuildCarrotMessageAsync(query, context, messageProperties, cancellationToken)
            .ConfigureAwait(false);

        return await SendReceiveAsync<TQuery, TResponse, TEndPointDefinition>(message, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SendAsync<TCommand, TResponse, TEndPointDefinition>(
        ICommand<TCommand, TResponse, TEndPointDefinition> command,
        ReplyEndPointBase? replyEndPoint = null,
        Context? context = null,
        MessageProperties? messageProperties = null,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default)
        where TResponse : class
        where TCommand : ICommand<TCommand, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        CarrotMessage message = await _messageBuilder
            .BuildCarrotMessageAsync(command, replyEndPoint, context, messageProperties, correlationId, cancellationToken)
            .ConfigureAwait(false);

        await SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SendAsync<TQuery, TResponse, TEndPointDefinition>(
        IQuery<TQuery, TResponse, TEndPointDefinition> query,
        ReplyEndPointBase replyEndPoint,
        Context? context = null,
        MessageProperties? messageProperties = null,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default)
        where TResponse : class
        where TQuery : IQuery<TQuery, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        CarrotMessage message = await _messageBuilder
            .BuildCarrotMessageAsync(query, replyEndPoint, context, messageProperties, correlationId, cancellationToken)
            .ConfigureAwait(false);

        await SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CarrotResponse<TRequest, TResponse>> SendReceiveAsync<TRequest, TResponse, TEndPointDefinition>(
        CarrotMessage message,
        CancellationToken cancellationToken)
        where TResponse : class
        where TRequest : _IRequest<TRequest, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new()
    {
        if (message.Header.MessageProperties.Ttl is null)
        {
            MessageProperties msgProps = message.Header.MessageProperties;
            msgProps.Ttl = 5_000;
            message.Header.MessageProperties = msgProps;
        }

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