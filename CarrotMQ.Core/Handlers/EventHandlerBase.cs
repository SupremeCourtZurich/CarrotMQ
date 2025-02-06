using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Handlers.HandlerResults;

namespace CarrotMQ.Core.Handlers;

/// <summary>
/// Represents a base class for event handlers. &lt;br /&gt;
/// Your EventHandler must inherit from this base class
/// </summary>
/// <typeparam name="TEvent">The type of the event being handled.</typeparam>
public abstract class EventHandlerBase<TEvent> : HandlerBase<TEvent, NoResponse>
    where TEvent : _IMessage<TEvent, NoResponse>
{
    /// <summary>
    /// Creates a handler result indicating that the event processing is successful.
    /// The message wil be acked
    /// </summary>
    public IHandlerResult Ok()
    {
        return new OkResult();
    }
}