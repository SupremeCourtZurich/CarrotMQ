using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CarrotMQ.Core.Common;
using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.Dto.Internals;
using Microsoft.Extensions.Logging;

namespace CarrotMQ.Core.Handlers;

/// <summary>
/// Represents a subscription to a CarrotMQ event of type <typeparamref name="TEvent" />.
/// </summary>
/// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
/// <remarks>You can register this subscription with <see cref="HandlerCollection.AddEventSubscription{TEvent}" /> when configuring the DI.</remarks>
public class EventSubscription<TEvent> where TEvent : _IMessage<TEvent, NoResponse>
{
    private readonly ILogger<EventSubscription<TEvent>> _logger;

    /// <summary>
    /// An event handler that is invoked when a CarrotMQ event of type <typeparamref name="TEvent" /> is received.
    /// </summary>
    public AsyncEventHandler<EventSubscriptionEventArgs<TEvent>>? EventReceived;

    /// 
    public EventSubscription(ILogger<EventSubscription<TEvent>> logger)
    {
        _logger = logger;
    }

    internal async Task OnEventReceived(TEvent message, ConsumerContext consumerContext)
    {
        if (EventReceived is null)
        {
            return;
        }

        var handlerExceptions = new List<Exception>();

        foreach (var messageCallback in EventReceived.GetHandlers())
        {
            try
            {
                await messageCallback.Invoke(this, new EventSubscriptionEventArgs<TEvent>(message, consumerContext)).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while handling event of type {EventType}", typeof(TEvent).FullName);
                handlerExceptions.Add(e);
            }
        }

        if (handlerExceptions.Count > 0)
        {
            throw new AggregateException("One or more event subscriptions threw exceptions", handlerExceptions);
        }
    }
}