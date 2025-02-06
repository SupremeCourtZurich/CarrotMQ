namespace CarrotMQ.Core.Dto.Internals;

#pragma warning disable IDE1006
// ReSharper disable InconsistentNaming
/// <summary>
/// internal version of the <see cref="ICommand{TCommand,TResponse,TEndPointDefinition}" /> interface without the endpoint
/// this interface is only used internally on the consumer side
/// </summary>
public interface _ICommand<TCommand, TResponse> : _IRequest<TCommand, TResponse>
    where TResponse : class
    where TCommand : _ICommand<TCommand, TResponse>;