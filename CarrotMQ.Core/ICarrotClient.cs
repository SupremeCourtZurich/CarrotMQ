using System;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Dto;
using CarrotMQ.Core.EndPoints;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core;

/// <summary>
/// Represents a messaging client designed for publishing events and sending commands/queries.
/// When paired with our CarrotMQ.RabbitMQ NuGet package, this client enables the seamless transmission of messages over
/// RabbitMQ.
/// </summary>
public interface ICarrotClient
{
    /// <summary>
    /// Publishes an event <see cref="ICustomRoutingEvent{TEvent}" /> with a customizable
    /// <see cref="ICustomRoutingEvent{TEvent}.Exchange" /> and <see cref="ICustomRoutingEvent{TEvent}.RoutingKey" />
    /// Note: This method may also throw exceptions from underlying system or library code (serializers or transport specific
    /// implementations).
    /// </summary>
    /// <typeparam name="TEvent">The type of the event to be published.</typeparam>
    /// <param name="event">The event instance to be published.</param>
    /// <param name="context">The optional context for the operation.</param>
    /// <param name="messageProperties">The optional MessageProperties.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown if the provided event has invalid properties.</exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation has been canceled by <paramref name="cancellationToken" />, or the message's TTL
    /// (<see cref="MessageProperties.Ttl" />) expired first. When the publish itself did not complete, the
    /// <see cref="Exception.InnerException" /> is a <see cref="PublishFailedException" /> whose
    /// <see cref="PublishFailedException.Reason" /> tells whether the broker was unreachable.
    /// </exception>
    /// <exception cref="PublishFailedException">
    /// Thrown if no connection to the broker could be established within the initial connection timeout, or, without
    /// publisher confirms, the channel was closed while the message was being sent.
    /// <see cref="PublishFailedException.Reason" /> tells whether the broker was unreachable.
    /// </exception>
    /// <exception cref="RetryLimitExceededException">
    /// Thrown if the message could not be confirmed within the defined number of retries.
    /// </exception>
    Task PublishAsync<TEvent>(
        ICustomRoutingEvent<TEvent> @event,
        Context? context = null,
        MessageProperties? messageProperties = null,
        CancellationToken cancellationToken = default)
        where TEvent : ICustomRoutingEvent<TEvent>;

    /// <summary>
    /// Publishes an event <see cref="IEvent{TEvent,TExchangeEndPoint}" />.
    /// Note: This method may also throw exceptions from underlying system or library code (serializers or transport specific
    /// implementations).
    /// </summary>
    /// <typeparam name="TEvent">The type of the event to be published.</typeparam>
    /// <typeparam name="TExchangeEndPoint">The type of the exchange endpoint associated with the event.</typeparam>
    /// <param name="event">The event instance to be published.</param>
    /// <param name="context">The optional context for the operation.</param>
    /// <param name="messageProperties">The optional MessageProperties. If not specified <see cref="MessageProperties.Default" /> is used.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown if the provided event has invalid properties.</exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation has been canceled by <paramref name="cancellationToken" />, or the message's TTL
    /// (<see cref="MessageProperties.Ttl" />) expired first. When the publish itself did not complete, the
    /// <see cref="Exception.InnerException" /> is a <see cref="PublishFailedException" /> whose
    /// <see cref="PublishFailedException.Reason" /> tells whether the broker was unreachable.
    /// </exception>
    /// <exception cref="PublishFailedException">
    /// Thrown if no connection to the broker could be established within the initial connection timeout, or, without
    /// publisher confirms, the channel was closed while the message was being sent.
    /// <see cref="PublishFailedException.Reason" /> tells whether the broker was unreachable.
    /// </exception>
    /// <exception cref="RetryLimitExceededException">
    /// Thrown if the message could not be confirmed within the defined number of retries.
    /// </exception>
    Task PublishAsync<TEvent, TExchangeEndPoint>(
        IEvent<TEvent, TExchangeEndPoint> @event,
        Context? context = null,
        MessageProperties? messageProperties = null,
        CancellationToken cancellationToken = default)
        where TEvent : IEvent<TEvent, TExchangeEndPoint>
        where TExchangeEndPoint : ExchangeEndPoint, new();

    /// <summary>
    /// Sends a command <see cref="ICommand{TCommand,TResponse,TEndPointDefinition}" /> and returns the reply.
    /// Note: This method may also throw exceptions from underlying system or library code (serializers or transport specific
    /// implementations).
    /// </summary>
    /// <typeparam name="TCommand">The type of the command to be sent.</typeparam>
    /// <typeparam name="TResponse">The type of the expected response.</typeparam>
    /// <typeparam name="TEndPointDefinition">The type of the endpoint definition associated with the command.</typeparam>
    /// <param name="command">The command instance to be sent.</param>
    /// <param name="context">The optional context for the operation.</param>
    /// <param name="messageProperties">
    /// The optional MessageProperties.
    /// If not specified <see cref="MessageProperties.Default" /> is used. A <see cref="MessageProperties.Ttl" /> of null is set to
    /// 5000; a TTL of zero or less is kept in the header, and the wait for the reply then ends only through
    /// <paramref name="cancellationToken" />, or after 5000 ms when that token cannot be cancelled.
    /// </param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The reply to the command.</returns>
    /// <exception cref="ArgumentException">Thrown if the provided command has invalid properties.</exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation has been canceled by <paramref name="cancellationToken" />, or the message's TTL
    /// (<see cref="MessageProperties.Ttl" />, or 5000 ms when it is not positive and no cancellable token is passed) expired first. When the publish itself did not complete, the
    /// <see cref="Exception.InnerException" /> is a <see cref="PublishFailedException" /> whose
    /// <see cref="PublishFailedException.Reason" /> tells whether the broker was unreachable.
    /// </exception>
    /// <exception cref="PublishFailedException">
    /// Thrown if no connection to the broker could be established within the initial connection timeout, or, without
    /// publisher confirms, the channel was closed while the message was being sent.
    /// <see cref="PublishFailedException.Reason" /> tells whether the broker was unreachable.
    /// </exception>
    /// <exception cref="RetryLimitExceededException">
    /// Thrown if the message could not be confirmed within the defined number of retries.
    /// </exception>
    Task<CarrotResponse<TCommand, TResponse>> SendReceiveAsync<TCommand, TResponse, TEndPointDefinition>(
        ICommand<TCommand, TResponse, TEndPointDefinition> command,
        Context? context = null,
        MessageProperties? messageProperties = null,
        CancellationToken cancellationToken = default)
        where TResponse : class
        where TCommand : ICommand<TCommand, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new();

    /// <summary>
    /// Sends a query <see cref="IQuery{TQuery,TResponse,TEndPointDefinition}" /> and returns the reply.
    /// Note: This method may also throw exceptions from underlying system or library code (serializers or transport specific
    /// implementations).
    /// </summary>
    /// <typeparam name="TQuery">The type of the query to be sent.</typeparam>
    /// <typeparam name="TResponse">The type of the expected response.</typeparam>
    /// <typeparam name="TEndPointDefinition">The type of the endpoint definition associated with the query.</typeparam>
    /// <param name="query">The query instance to be sent.</param>
    /// <param name="context">The optional context for the operation.</param>
    /// <param name="messageProperties">
    /// The optional MessageProperties.
    /// If not specified <see cref="MessageProperties.Default" /> is used. A <see cref="MessageProperties.Ttl" /> of null is set to
    /// 5000; a TTL of zero or less is kept in the header, and the wait for the reply then ends only through
    /// <paramref name="cancellationToken" />, or after 5000 ms when that token cannot be cancelled.
    /// </param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The reply to the query.</returns>
    /// <exception cref="ArgumentException">Thrown if the provided query has invalid properties.</exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation has been canceled by <paramref name="cancellationToken" />, or the message's TTL
    /// (<see cref="MessageProperties.Ttl" />, or 5000 ms when it is not positive and no cancellable token is passed) expired first. When the publish itself did not complete, the
    /// <see cref="Exception.InnerException" /> is a <see cref="PublishFailedException" /> whose
    /// <see cref="PublishFailedException.Reason" /> tells whether the broker was unreachable.
    /// </exception>
    /// <exception cref="PublishFailedException">
    /// Thrown if no connection to the broker could be established within the initial connection timeout, or, without
    /// publisher confirms, the channel was closed while the message was being sent.
    /// <see cref="PublishFailedException.Reason" /> tells whether the broker was unreachable.
    /// </exception>
    /// <exception cref="RetryLimitExceededException">
    /// Thrown if the message could not be confirmed within the defined number of retries.
    /// </exception>
    Task<CarrotResponse<TQuery, TResponse>> SendReceiveAsync<TQuery, TResponse, TEndPointDefinition>(
        IQuery<TQuery, TResponse, TEndPointDefinition> query,
        Context? context = null,
        MessageProperties? messageProperties = null,
        CancellationToken cancellationToken = default)
        where TResponse : class
        where TQuery : IQuery<TQuery, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new();

    /// <summary>
    /// Sends a command <see cref="ICommand{TCommand,TResponse,TEndPointDefinition}" />.
    /// Depending on <paramref name="replyEndPoint" /> a reply is sent asynchronously or not at all.
    /// Note: This method may also throw exceptions from underlying system or library code (serializers or transport specific
    /// implementations).
    /// </summary>
    /// <typeparam name="TCommand">The type of the command to be sent.</typeparam>
    /// <typeparam name="TResponse">The type of the expected response.</typeparam>
    /// <typeparam name="TEndPointDefinition">The type of the endpoint definition associated with the command.</typeparam>
    /// <param name="command">The command instance to be sent.</param>
    /// <param name="replyEndPoint">The optional reply endpoint for the command.</param>
    /// <param name="context">The optional context for the operation.</param>
    /// <param name="messageProperties">The optional MessageProperties. If not specified <see cref="MessageProperties.Default" /> is used.</param>
    /// <param name="correlationId">The optional correlation ID for the operation.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown if the provided command has invalid properties.</exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation has been canceled by <paramref name="cancellationToken" />, or the message's TTL
    /// (<see cref="MessageProperties.Ttl" />) expired first. When the publish itself did not complete, the
    /// <see cref="Exception.InnerException" /> is a <see cref="PublishFailedException" /> whose
    /// <see cref="PublishFailedException.Reason" /> tells whether the broker was unreachable.
    /// </exception>
    /// <exception cref="PublishFailedException">
    /// Thrown if no connection to the broker could be established within the initial connection timeout, or, without
    /// publisher confirms, the channel was closed while the message was being sent.
    /// <see cref="PublishFailedException.Reason" /> tells whether the broker was unreachable.
    /// </exception>
    /// <exception cref="RetryLimitExceededException">
    /// Thrown if the message could not be confirmed within the defined number of retries.
    /// </exception>
    Task SendAsync<TCommand, TResponse, TEndPointDefinition>(
        ICommand<TCommand, TResponse, TEndPointDefinition> command,
        ReplyEndPointBase? replyEndPoint = null,
        Context? context = null,
        MessageProperties? messageProperties = null,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default)
        where TResponse : class
        where TCommand : ICommand<TCommand, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new();

    /// <summary>
    /// Sends a query <see cref="IQuery{TQuery,TResponse,TEndPointDefinition}" />.
    /// The reply is sent asynchronously to <paramref name="replyEndPoint" />.
    /// Note: This method may also throw exceptions from underlying system or library code (serializers or transport specific
    /// implementations).
    /// </summary>
    /// <typeparam name="TQuery">The type of the query to be sent.</typeparam>
    /// <typeparam name="TResponse">The type of the expected response.</typeparam>
    /// <typeparam name="TEndPointDefinition">The type of the endpoint definition associated with the query.</typeparam>
    /// <param name="query">The query instance to be sent.</param>
    /// <param name="replyEndPoint">The reply endpoint for the query.</param>
    /// <param name="context">The optional context for the operation.</param>
    /// <param name="messageProperties">The optional MessageProperties. If not specified <see cref="MessageProperties.Default" /> is used.</param>
    /// <param name="correlationId">The optional correlation ID for the operation.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown if the provided query has invalid properties.</exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown if the operation has been canceled by <paramref name="cancellationToken" />, or the message's TTL
    /// (<see cref="MessageProperties.Ttl" />) expired first. When the publish itself did not complete, the
    /// <see cref="Exception.InnerException" /> is a <see cref="PublishFailedException" /> whose
    /// <see cref="PublishFailedException.Reason" /> tells whether the broker was unreachable.
    /// </exception>
    /// <exception cref="PublishFailedException">
    /// Thrown if no connection to the broker could be established within the initial connection timeout, or, without
    /// publisher confirms, the channel was closed while the message was being sent.
    /// <see cref="PublishFailedException.Reason" /> tells whether the broker was unreachable.
    /// </exception>
    /// <exception cref="RetryLimitExceededException">
    /// Thrown if the message could not be confirmed within the defined number of retries.
    /// </exception>
    Task SendAsync<TQuery, TResponse, TEndPointDefinition>(
        IQuery<TQuery, TResponse, TEndPointDefinition> query,
        ReplyEndPointBase replyEndPoint,
        Context? context = null,
        MessageProperties? messageProperties = null,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default)
        where TResponse : class
        where TQuery : IQuery<TQuery, TResponse, TEndPointDefinition>
        where TEndPointDefinition : EndPointBase, new();
}