using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.RabbitMQ.Configuration.Exchanges;
using CarrotMQ.RabbitMQ.Configuration.Queues;

namespace CarrotMQ.RabbitMQ.Configuration;

/// <summary>
/// Provides extensions for configuring the bindings between exchanges and queues.
/// </summary>
public static class HandlerExtensions
{
    /// <inheritdoc cref="Handler{TMessage,TResponse}" />
    extension<TMessage, TResponse>(Handler<TMessage, TResponse> handler) where TMessage : _IMessage<TMessage, TResponse> where TResponse : class
    {
        /// <summary>
        /// Bind the given <paramref name="exchange" /> to the given <paramref name="queue" /> using the <typeparamref name="TMessage" /> as
        /// routing key
        /// </summary>
        public Handler<TMessage, TResponse> BindTo(DirectExchange exchange, Queue queue)
        {
            return handler.BindTo(exchange.Name, queue.QueueName);
        }

        /// <summary>
        /// Bind the given <paramref name="exchange" /> to the given <paramref name="queue" /> using the <typeparamref name="TMessage" /> as
        /// routing key
        /// </summary>
        public Handler<TMessage, TResponse> BindTo(TopicExchange exchange, Queue queue)
        {
            return handler.BindTo(exchange.Name, queue.QueueName);
        }
    }
}