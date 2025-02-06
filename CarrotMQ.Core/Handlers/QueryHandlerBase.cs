using CarrotMQ.Core.Dto.Internals;

namespace CarrotMQ.Core.Handlers;

/// <summary>
/// Represents a base class for query handlers. <br />
/// Your QueryHandler must inherit from this base class
/// </summary>
/// <typeparam name="TQuery">The type of the query being handled.</typeparam>
/// <typeparam name="TResponse">The type of the response produced by the query handler.</typeparam>
public abstract class QueryHandlerBase<TQuery, TResponse> : RequestHandlerBase<TQuery, TResponse>
    where TQuery : _IQuery<TQuery, TResponse> where TResponse : class;