namespace CarrotMQ.Core.Dto.Internals;

#pragma warning disable IDE1006
// ReSharper disable InconsistentNaming
/// <summary>
/// internal version of the <see cref="IEvent{TEvent,TExchangeEndPoint}" /> interface without the exchange endpoint
/// this interface is only used internally on the consumer side
/// </summary>
/// <typeparam name="TEvent"></typeparam>
public interface _IEvent<TEvent> : _IMessage<TEvent, NoResponse>
    where TEvent : _IEvent<TEvent>;