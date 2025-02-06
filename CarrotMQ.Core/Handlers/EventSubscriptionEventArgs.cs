using System;

namespace CarrotMQ.Core.Handlers;

/// <summary>
/// Provides data for the <see cref="EventSubscription{TEvent}.EventReceived" /> event.
/// </summary>
/// <typeparam name="TEvent">The type of the CarrotMQ event.</typeparam>
public class EventSubscriptionEventArgs<TEvent> : EventArgs
{
    ///
    public EventSubscriptionEventArgs(TEvent message, ConsumerContext consumerContext)
    {
        Event = message;
        ConsumerContext = consumerContext;
    }

    /// <summary>
    /// CarrotMQ event message.
    /// </summary>
    public TEvent Event { get; }

    ///
    public ConsumerContext ConsumerContext { get; }
}