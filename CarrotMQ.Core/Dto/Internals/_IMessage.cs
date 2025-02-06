using CarrotMQ.Core.EndPoints;

#pragma warning disable IDE1006
// ReSharper disable InconsistentNaming
namespace CarrotMQ.Core.Dto.Internals;

/// <summary>
/// Internal interface
/// </summary>
public interface _IMessage<TMessage, TResponse>
    where TResponse : class
    where TMessage : _IMessage<TMessage, TResponse>;

/// <summary>
/// Internal interface
/// </summary>
public interface _IMessage<TMessage, TResponse, TEndPointDefinition> : _IMessage<TMessage, TResponse>
    where TResponse : class
    where TMessage : _IMessage<TMessage, TResponse, TEndPointDefinition>
    where TEndPointDefinition : EndPointBase, new();