using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Handlers.HandlerResults;

namespace CarrotMQ.Core.Handlers;

/// <summary>
/// Base class to handle responses of a specific request.
/// </summary>
/// <typeparam name="TRequest">The type of the original request being handled.</typeparam>
/// <typeparam name="TResponse">The type of the response being handled.</typeparam>
public abstract class ResponseHandlerBase<TRequest, TResponse>
    : HandlerBase<CarrotResponse<TRequest, TResponse>, NoResponse>
    where TResponse : class
    where TRequest : _IRequest<TRequest, TResponse>
{
    /// <summary>
    /// Creates a handler result indicating that the event processing is successful.
    /// The message will be acked
    /// </summary>
    public IHandlerResult Ok()
    {
        return new OkResult();
    }
}