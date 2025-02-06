using CarrotMQ.Core.EndPoints;

#pragma warning disable IDE1006
// ReSharper disable InconsistentNaming
namespace CarrotMQ.Core.Dto.Internals;

/// <summary>
/// Internal interface
/// </summary>
public interface _IRequest<TRequest, TResponse> : _IMessage<TRequest, TResponse>
    where TResponse : class
    where TRequest : _IRequest<TRequest, TResponse>;

/// <summary>
/// Internal interface
/// </summary>
public interface _IRequest<TRequest, TResponse, TEndPointDefinition>
    : _IRequest<TRequest, TResponse>, _IMessage<TRequest, TResponse, TEndPointDefinition>
    where TResponse : class
    where TRequest : _IRequest<TRequest, TResponse, TEndPointDefinition>
    where TEndPointDefinition : EndPointBase, new();