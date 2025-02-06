using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.EndPoints;

namespace CarrotMQ.Core.Dto;

/// <summary>
/// Represents an event interface that defines the contract for handling a specific event
/// sent over an exchange endpoint.
/// </summary>
/// <typeparam name="TEvent">The type of the event.</typeparam>
/// <typeparam name="TExchangeEndPoint">
/// The type of the messaging exchange endpoint associated with the event (should
/// inherit from <see cref="ExchangeEndPoint" />)
/// </typeparam>
/// <example>
/// Example of an event definition:
/// <code>
/// public class MyEvent : IEvent&lt;MyEvent, MyExchangeEndPoint&gt;
/// {
///     public MyEvent(string eventData)
///     {
///         EventData = eventData;
///     }
/// 
///     public string EventData { get; set; }
/// }</code>
/// </example>
public interface IEvent<TEvent, TExchangeEndPoint> :
    _IEvent<TEvent>,
    _IMessage<TEvent, NoResponse, TExchangeEndPoint>
    where TEvent : IEvent<TEvent, TExchangeEndPoint>
    where TExchangeEndPoint : ExchangeEndPoint, new();