using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.RabbitMQ.Configuration.Exchanges;
using CarrotMQ.RabbitMQ.Configuration.Queues;

namespace CarrotMQ.RabbitMQ.Configuration;

/// <summary>
/// Provides a builder for configuring the bindings between exchanges and queues.
/// </summary>
public static class BindingConfigurationBuilderExtensions
{
    /// <summary>
    /// Bind the given <paramref name="exchangeBuilder" /> to the given <paramref name="queueBuilder" /> using the <typeparamref name="TMessage" /> as
    /// routing key
    /// </summary>
    public static Handler<TMessage, TResponse> BindTo<TMessage, TResponse>(
        this Handler<TMessage, TResponse> handler,
        DirectExchangeBuilder exchangeBuilder,
        Queue queueBuilder)
        where TMessage : _IMessage<TMessage, TResponse>
        where TResponse : class
    {
        return handler.BindTo(exchangeBuilder.Configuration.Name, queueBuilder.QueueName);
    }

    /// <summary>
    /// Bind the given <paramref name="exchangeBuilder" /> to the given <paramref name="queueBuilder" /> using the <typeparamref name="TMessage" /> as
    /// routing key
    /// </summary>
    public static Handler<TMessage, TResponse> BindTo<TMessage, TResponse>(
        this Handler<TMessage, TResponse> handler,
        TopicExchangeBuilder exchangeBuilder,
        Queue queueBuilder)
        where TMessage : _IMessage<TMessage, TResponse>
        where TResponse : class
    {
        return handler.BindTo(exchangeBuilder.Name, queueBuilder.QueueName);
    }
}