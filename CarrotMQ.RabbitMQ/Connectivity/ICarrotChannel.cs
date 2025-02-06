using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CarrotMQ.Core.Configuration;
using CarrotMQ.RabbitMQ.Configuration.Exchanges;
using CarrotMQ.RabbitMQ.Configuration.Queues;
using RabbitMQ.Client.Exceptions;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// The abstraction of the RabbitMQ channel.
/// <see href="https://www.rabbitmq.com/dotnet.html" />
/// </summary>
public interface ICarrotChannel : IAsyncDisposable
{
    /// <summary>
    /// True, if channel is open and available for use.
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// True, if channel is closed and not available for use.
    /// </summary>
    bool IsClosed { get; }

    /// <summary>
    /// Creates a queue. (idempotent)
    /// </summary>
    /// <param name="queueName">Name of the queue.</param>
    /// <param name="durable">If true, the queue will be durable.</param>
    /// <param name="exclusive">If true, the queue will be exclusive.</param>
    /// <param name="autoDelete">If true, the queue will be auto deleted.</param>
    /// <param name="arguments">Additional arguments (null = none).</param>
    /// <exception cref="OperationInterruptedException"></exception>
    Task DeclareQueueAsync(string queueName, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object?>? arguments = null);

    /// <summary>
    /// Creates the queue if required and creates bindings with the given list.
    /// </summary>
    Task ApplyConfigurations(QueueConfiguration queueConfig, IList<BindingConfiguration> bindings);

    /// <summary>
    /// Deletes a queue.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <exception cref="OperationInterruptedException"></exception>
    Task DeleteQueueAsync(string queueName);

    /// <summary>
    /// Creates an exchange. (idempotent)
    /// </summary>
    /// <param name="exchangeName">Name of the exchange.</param>
    /// <param name="exchangeType">Type of the exchange.</param>
    /// <param name="durable">
    /// If true, the exchange will be marked as durable. Durable exchanges remain active when a server
    /// restarts. Non­durable exchanges(transient exchanges) are purged when a server restarts.
    /// </param>
    /// <param name="autoDelete">If true, the exchange will be auto deleted one it is unused (no open channels, bindings).</param>
    /// <param name="arguments">Additional arguments (null = none).</param>
    /// <exception cref="OperationInterruptedException"></exception>
    Task DeclareExchangeAsync(
        string exchangeName,
        string exchangeType,
        bool durable,
        bool autoDelete,
        IDictionary<string, object?>? arguments = null);

    /// <summary>
    /// Creates all exchanges with the given list.
    /// </summary>
    Task DeclareExchangesAsync(ExchangeCollection exchangeCollection);

    /// <summary>
    /// Deletes an exchange.
    /// </summary>
    /// <param name="exchangeName">Name of the exchange to delete</param>
    /// <param name="ifUnused">Delete only if unused</param>
    /// <exception cref="OperationInterruptedException"></exception>
    Task DeleteExchangeAsync(string exchangeName, bool ifUnused = false);

    /// <summary>
    /// Binds a queue to an exchange under a given routingKey.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <param name="exchange">The name of the exchange.</param>
    /// <param name="routingKey">The routing key.</param>
    /// <param name="arguments">Additional AMQP arguments.</param>
    /// <exception cref="OperationInterruptedException"></exception>
    Task BindQueueAsync(string queueName, string exchange, string routingKey, IDictionary<string, object?>? arguments = null);

    /// <summary>
    /// Checks if a queue exists.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <returns>True, if the queue exists on the broker, otherwise false.</returns>
    /// <exception cref="OperationInterruptedException"></exception>
    Task<bool> CheckQueueAsync(string queueName);

    /// <summary>
    /// Checks if an exchange exists.
    /// </summary>
    /// <param name="exchangeName">The name of the exchange.</param>
    /// <returns>True, if the exchange exists on the broker, otherwise false.</returns>
    /// <exception cref="OperationInterruptedException"></exception>
    Task<bool> CheckExchangeAsync(string exchangeName);

    /// <summary>
    /// Event that occurs when a transport error is received on the channel.
    /// </summary>
    public event EventHandler<TransportErrorReceivedEventArgs>? TransportErrorReceived;
}