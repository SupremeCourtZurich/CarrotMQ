using CarrotMQ.Core.Dto.Internals;

namespace CarrotMQ.Core.Dto;

/// <summary>
/// Represents an event interface that defines the contract for handling a specific event
/// with custom routing information.
/// </summary>
/// <typeparam name="TEvent">The type of the custom routing event.</typeparam>
/// <seealso cref="_IEvent{TEvent}" />
/// <example>
/// Example of an event definition:
/// <code>
///  public class MyCustomRoutingEvent : ICustomRoutingEvent&lt;MyCustomRoutingEvent&gt;
///  {
///      public MyCustomRoutingEvent(string exchange, string routingKey, string eventData)
///      {
///          EventData = eventData;
///          Exchange = exchange;
///          RoutingKey = routingKey;
///      }
/// 
///      public string EventData { get; set; }
///  
///      public string Exchange { get; set; }
///  
///      public string RoutingKey { get; set; }
///  }
/// </code>
/// </example>
public interface ICustomRoutingEvent<TEvent> :
    _IMessage<TEvent, NoResponse>
    where TEvent : ICustomRoutingEvent<TEvent>
{
    /// <summary>
    /// Exchange where the event will be sent to
    /// </summary>
    public string Exchange { get; set; }

    /// <summary>
    /// Routing key used by RabbitMQ to route the message to the right queue
    /// </summary>
    public string RoutingKey { get; set; }
}