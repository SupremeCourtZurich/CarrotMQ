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
    private const int DefaultRequestTtlInMs = 5_000;
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
        var message = await _messageBuilder.BuildCarrotMessageAsync(@event, context, messageProperties, cancellationToken).ConfigureAwait(false);

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
        var message = await _messageBuilder.BuildCarrotMessageAsync(@event, context, messageProperties, cancellationToken).ConfigureAwait(false);

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
        var message = await _messageBuilder.BuildCarrotMessageAsync(command, context, messageProperties, cancellationToken).ConfigureAwait(false);

        return await SendReceiveAsync<TCommand, TResponse, TEndPointDefinition>(message, cancellationToken).ConfigureAwait(false);
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
        var message = await _messageBuilder.BuildCarrotMessageAsync(query, context, messageProperties, cancellationToken).ConfigureAwait(false);

        return await SendReceiveAsync<TQuery, TResponse, TEndPointDefinition>(message, cancellationToken).ConfigureAwait(false);
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
        var message = await _messageBuilder
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
        var message = await _messageBuilder
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
            msgProps.Ttl = DefaultRequestTtlInMs;
            message.Header.MessageProperties = msgProps;
        }

        CarrotMessage responseMessage;

        // Without a positive TTL the wait for the reply is still bounded by the default TTL.
        var ttl = message.Header.MessageProperties.Ttl is > 0 ? message.Header.MessageProperties.Ttl.Value : DefaultRequestTtlInMs;
        using (var ttlSource = CreateTtlSource(ttl, cancellationToken))
        {
            var token = ttlSource.Token;
            try
            {
                responseMessage = await _transport.SendReceiveAsync(message, token).ConfigureAwait(false);
            }
            catch (PublishFailedException ex) when (ex.InnerException is OperationCanceledException && token.IsCancellationRequested)
            {
                throw Cancelled(ex, cancellationToken, token);
            }
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
        using var ttlSource = message.Header.MessageProperties.Ttl is > 0 ? CreateTtlSource(message.Header.MessageProperties.Ttl.Value, cancellationToken) : null;
        var token = ttlSource?.Token ?? cancellationToken;
        try
        {
            await _transport.SendAsync(message, token).ConfigureAwait(false);
        }
        catch (PublishFailedException ex) when (ex.InnerException is OperationCanceledException && token.IsCancellationRequested)
        {
            throw Cancelled(ex, cancellationToken, token);
        }
    }

    private static CancellationTokenSource CreateTtlSource(int ttlInMs, CancellationToken cancellationToken)
    {
        var ttlSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ttlSource.CancelAfter(ttlInMs);

        return ttlSource;
    }

    // Caller cancellation and TTL expiry both end in OperationCanceledException; the inner exception carries the reason.
    private static OperationCanceledException Cancelled(PublishFailedException exception, CancellationToken callerToken, CancellationToken token)
    {
        return new OperationCanceledException(exception.Message, exception, callerToken.IsCancellationRequested ? callerToken : token);
    }
}