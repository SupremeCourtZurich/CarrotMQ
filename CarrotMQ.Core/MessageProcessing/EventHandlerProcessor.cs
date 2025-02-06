using System;
using System.Threading.Tasks;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.MessageProcessing.Middleware;
using CarrotMQ.Core.Serialization;

namespace CarrotMQ.Core.MessageProcessing;

/// <summary>
/// Caller proxy which calls the EventHandler transforming the object arguments into the generic argument types
/// (TRequest, TResponse)
/// </summary>
internal sealed class EventHandlerProcessor<TEventHandler, TEvent> : HandlerProcessorBase
    where TEventHandler : EventHandlerBase<TEvent>
    where TEvent : _IMessage<TEvent, NoResponse>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventHandlerProcessor{TEventHandler, TEvent}" /> class.
    /// </summary>
    /// <remarks>
    /// The constructor sets up the processor with the called method key resolved based on the specified
    /// <typeparamref name="TEvent" /> type.
    /// </remarks>
    internal EventHandlerProcessor() :
        base(CalledMethodResolver.BuildCalledMethodKey(typeof(TEvent)))
    {
    }

    internal override Type MessageType => typeof(TEvent);

    internal override Type HandlerType => typeof(TEventHandler);

    /// <inheritdoc />
    internal override async Task HandleAsync(MiddlewareContext middlewareContext, IDependencyInjector scopedDependencyInjector)
    {
        var eventHandler = GetHandler<TEventHandler, TEvent, NoResponse>(scopedDependencyInjector);
        var serializer = scopedDependencyInjector.GetCarrotSerializer();
        var eventMessage = serializer.DeserializeWithNullCheck<TEvent>(middlewareContext.Message.Payload);

        var result = await eventHandler.HandleAsync(eventMessage, middlewareContext.ConsumerContext, middlewareContext.CancellationToken)
            .ConfigureAwait(false);

        middlewareContext.DeliveryStatus = result.DeliveryStatus;
    }
}