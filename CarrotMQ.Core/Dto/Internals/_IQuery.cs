namespace CarrotMQ.Core.Dto.Internals;

#pragma warning disable IDE1006
// ReSharper disable InconsistentNaming
/// <summary>
/// internal version of the <see cref="IQuery{TQuery,TResponse,TEndPointDefinition}" /> interface without the endpoint
/// this interface is only used internally on the consumer side
/// </summary>
public interface _IQuery<TQuery, TResponse> : _IRequest<TQuery, TResponse>
    where TResponse : class
    where TQuery : _IQuery<TQuery, TResponse>;