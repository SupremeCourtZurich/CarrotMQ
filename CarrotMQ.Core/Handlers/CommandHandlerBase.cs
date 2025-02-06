using CarrotMQ.Core.Dto.Internals;

namespace CarrotMQ.Core.Handlers;

/// <summary>
/// Represents a base class for command handlers. <br />
/// Your CommandHandler must inherit from this base class
/// </summary>
/// <typeparam name="TCommand">The type of the command being handled.</typeparam>
/// <typeparam name="TResponse">The type of the response produced by the command handler.</typeparam>
public abstract class CommandHandlerBase<TCommand, TResponse> : RequestHandlerBase<TCommand, TResponse>
    where TCommand : _ICommand<TCommand, TResponse> where TResponse : class;