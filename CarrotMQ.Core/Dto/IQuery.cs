using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.EndPoints;

namespace CarrotMQ.Core.Dto;

/// <summary>
/// Represents a query interface that defines the contract for handling a specific query
/// and its corresponding response sent over a specific endpoint (Exchange or Queue).
/// </summary>
/// <typeparam name="TQuery">The type of the query.</typeparam>
/// <typeparam name="TResponse">The type of the query response.</typeparam>
/// <typeparam name="TEndPointDefinition">
/// The type of the endpoint definition where the query will be sent to
/// (should inherit from <see cref="ExchangeEndPoint" /> or <see cref="QueueEndPoint" />).
/// </typeparam>
/// <seealso cref="_IRequest{TQuery,TResponse,TEndPointDefinition}" />
/// <seealso cref="_IQuery{TQuery, TResponse}" />
/// <example>
/// Example of a query definition
/// <code>
/// public class RetrieveDataQuery : IQuery&lt;RetrieveDataQuery, RetrieveDataQuery.Response, MyExchangeEndPoint&gt;
/// {
///     public RetrieveDataQuery(string queryParameter)
///     {
///         QueryParameter = queryParameter;
///     }
/// 
///     public string QueryParameter { get; set; }
/// 
///     public class Response
///     {
///         public string ResultData { get; set; }
///     }
/// }
/// </code>
/// </example>
public interface IQuery<TQuery, TResponse, TEndPointDefinition>
    : _IRequest<TQuery, TResponse, TEndPointDefinition>, _IQuery<TQuery, TResponse>
    where TResponse : class
    where TQuery : IQuery<TQuery, TResponse, TEndPointDefinition>
    where TEndPointDefinition : EndPointBase, new();