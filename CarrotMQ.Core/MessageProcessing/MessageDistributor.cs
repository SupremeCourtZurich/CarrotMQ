using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.MessageProcessing.Middleware;
using CarrotMQ.Core.Protocol;
using Microsoft.Extensions.Logging;

namespace CarrotMQ.Core.MessageProcessing;

/// <summary>
/// Distributes messages to registered handlers (<see cref="HandlerBase{TMessage,TResponse}" />) and/or callback functions
/// associated with CarrotMQ messages.
/// </summary>
internal sealed class MessageDistributor : IMessageDistributor
{
    private readonly IDependencyInjector _dependencyInjector;
    private readonly IDictionary<string, HandlerProcessorBase> _handlerRegistry;

    private readonly ILogger<MessageDistributor> _logger;
    private readonly IResponseSender _responseSender;

    /// 
    public MessageDistributor(
        IDependencyInjector dependencyInjector,
        HandlerCollection handlerCollection,
        IResponseSender responseSender,
        ILogger<MessageDistributor> logger)
    {
        _dependencyInjector = dependencyInjector;
        _logger = logger;
        _responseSender = responseSender;

        _handlerRegistry = handlerCollection.GetHandlers();
    }

    /// <inheritdoc />
    public async Task<DeliveryStatus> DistributeAsync(CarrotMessage carrotMessage, CancellationToken cancellationToken)
    {
        var handlerExists = _handlerRegistry.TryGetValue(carrotMessage.Header.CalledMethod, out var handlerProcessor);
        if (!handlerExists)
        {
            _logger.LogError("No handler registered for {CalledMethod}", carrotMessage.Header.CalledMethod);

            return DeliveryStatus.Reject;
        }

        MiddlewareContext? middlewareContext = null;

        try
        {
            var scopedDependencyInjector = _dependencyInjector.CreateAsyncScope();
            await using var _ = scopedDependencyInjector.ConfigureAwait(false);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (carrotMessage.Header.MessageProperties.Ttl > 0)
            {
                cts.CancelAfter((int)carrotMessage.Header.MessageProperties.Ttl);
            }

            middlewareContext = CreateMiddlewareContext(carrotMessage, handlerProcessor!, cts.Token);

            async Task HandlerActionAsync()
            {
                await handlerProcessor!.HandleAsync(middlewareContext, scopedDependencyInjector).ConfigureAwait(false);
            }

            var middlewareProcessor = scopedDependencyInjector.GetMiddlewareProcessor();
            await middlewareProcessor.RunAsync(middlewareContext, HandlerActionAsync).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while handling message: {Payload}", carrotMessage.Payload);

            if (middlewareContext is { IsErrorResult: false })
            {
                var error = new CarrotError("Unknown error occurred during message processing! Check the logs for more information");

                middlewareContext.HandlerResult = new ErrorResult(error, response: null);
            }
        }
        finally
        {
            if (middlewareContext is not null)
            {
                await _responseSender.TrySendResponseAsync(middlewareContext).ConfigureAwait(false);
            }
        }

        return middlewareContext?.DeliveryStatus ?? DeliveryStatus.Reject;
    }

    private MiddlewareContext CreateMiddlewareContext(
        CarrotMessage carrotMessage,
        HandlerProcessorBase handlerProcessor,
        CancellationToken cancellationToken)
    {
        var consumerContext = new ConsumerContext(
            carrotMessage.Header.InitialUserName,
            carrotMessage.Header.InitialServiceName,
            carrotMessage.Header.MessageProperties,
            carrotMessage.Header.CustomHeader,
            carrotMessage.Header.MessageId,
            carrotMessage.Header.CorrelationId,
            carrotMessage.Header.CreatedAt);

        return
            new MiddlewareContext(carrotMessage, handlerProcessor.MessageType, consumerContext, cancellationToken)
            {
                HandlerType = handlerProcessor.HandlerType
            };
    }
}