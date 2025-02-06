using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.EndPoints;

namespace CarrotMQ.Core.Dto;

/// <summary>
/// Represents a command interface that defines the contract for handling a specific command
/// and its corresponding response sent over a specific endpoint (Exchange or Queue).
/// </summary>
/// <typeparam name="TCommand">The type of the command.</typeparam>
/// <typeparam name="TResponse">The type of the command response.</typeparam>
/// <typeparam name="TEndPointDefinition">
/// The type of the endpoint definition where the command will be sent to
/// (should inherit from <see cref="ExchangeEndPoint" /> or <see cref="QueueEndPoint" />).
/// </typeparam>
/// <example>
/// Example of a command definition
/// <code>
/// public class ExecuteSomethingCmd : ICommand&lt;ExecuteSomethingCmd, ExecuteSomethingCmd.Response, MyQueueEndPoint&gt;
/// {
///     public ExecuteSomethingCmd(string cmdData)
///     {
///         CmdData = cmdData;
///     }
/// 
///     public string CmdData { get; set; }
/// 
/// 
///     public class Response
///     {
///         public bool Success { get; set; }
///     }
/// }
/// </code>
/// </example>
public interface ICommand<TCommand, TResponse, TEndPointDefinition>
    : _IRequest<TCommand, TResponse, TEndPointDefinition>, _ICommand<TCommand, TResponse>
    where TResponse : class
    where TCommand : ICommand<TCommand, TResponse, TEndPointDefinition>
    where TEndPointDefinition : EndPointBase, new();