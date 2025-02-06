using System.Collections.Generic;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core.Handlers;

/// <summary>
/// Base class for <see cref="QueryHandlerBase{TQuery,TResponse}" /> and
/// <see cref="QueryHandlerBase{TQuery,TResponse}" />.
/// </summary>
/// <typeparam name="TRequest">The type of the request being handled.</typeparam>
/// <typeparam name="TResponse">The type of the response produced by the handler.</typeparam>
/// <remarks>
/// You should never directly inherit from this class. Use
/// <see cref="QueryHandlerBase{TQuery,TResponse}" /> or <see cref="CommandHandlerBase{TCommand,TResponse}" /> instead
/// </remarks>
public abstract class RequestHandlerBase<TRequest, TResponse> : HandlerBase<TRequest, TResponse>
    where TRequest : _IRequest<TRequest, TResponse>
    where TResponse : class
{
    /// <summary>
    /// Creates a handler result indicating that the event processing has been canceled.
    /// </summary>
    /// <returns>
    /// A <see cref="IHandlerResult" /> indicating an error with status code
    /// <see cref="CarrotStatusCode.GatewayTimeout" />.
    /// </returns>
    public IHandlerResult Cancel()
    {
        return Error(CarrotStatusCode.GatewayTimeout);
    }

    /// <summary>
    /// Creates a handler result indicating that the event processing is successful.
    /// The message will be acked and the <typeparamref name="TResponse" /> will be sent.
    /// </summary>
    public IHandlerResult Ok(TResponse response)
    {
        return new OkResult(response);
    }

    /// <summary>
    /// Returns a 500 - internal server error with optional error message and field specific validation errors
    /// </summary>
    /// <param name="errorMessage">The optional error message.</param>
    /// <param name="validationErrors">The optional field-specific validation errors.</param>
    /// <returns>A <see cref="IHandlerResult" /> representing a 500 - internal server error.</returns>
    public IHandlerResult Error(string? errorMessage = null, IDictionary<string, string[]>? validationErrors = null)
    {
        return Error(null, errorMessage, validationErrors);
    }

    /// <summary>
    /// Returns a 500 - internal server error with optional response object <typeparamref name="TResponse" />, error message
    /// and field specific validation errors
    /// </summary>
    /// <param name="response">The optional response object of type <typeparamref name="TResponse" />.</param>
    /// <param name="errorMessage">The optional error message.</param>
    /// <param name="validationErrors">The optional field-specific validation errors.</param>
    /// <returns>A <see cref="IHandlerResult" /> representing a 500 - internal server error.</returns>
    public IHandlerResult Error(TResponse? response, string? errorMessage = null, IDictionary<string, string[]>? validationErrors = null)
    {
        return Error(CarrotStatusCode.InternalServerError, response, errorMessage, validationErrors);
    }

    /// <summary>
    /// Returns a response with the given status code with optional response object <typeparamref name="TResponse" />, error
    /// message and
    /// field specific validation errors
    /// </summary>
    /// <param name="statusCode">The HTTP status code for the error response.</param>
    /// <param name="response">The optional response object.</param>
    /// <param name="errorMessage">The optional error message.</param>
    /// <param name="validationErrors">The optional field-specific validation errors.</param>
    /// <returns>A <see cref="IHandlerResult" /> representing an error response with the specified status code.</returns>
    public IHandlerResult Error(
        int statusCode,
        TResponse? response = null,
        string? errorMessage = null,
        IDictionary<string, string[]>? validationErrors = null)
    {
        CarrotError? error = null;

        if (!string.IsNullOrEmpty(errorMessage) || (validationErrors != null && validationErrors.Count != 0))
        {
            error = new CarrotError(errorMessage ?? string.Empty, validationErrors);
        }

        return new ErrorResult(error, response, statusCode);
    }

    /// <summary>
    /// Returns a 400 - bad request error with optional error message and field specific validation errors
    /// </summary>
    /// <param name="errorMessage">The optional error message.</param>
    /// <param name="validationErrors">The optional field-specific validation errors.</param>
    /// <returns>A <see cref="IHandlerResult" /> representing a 400 - bad request error.</returns>
    public IHandlerResult BadRequest(string? errorMessage = null, IDictionary<string, string[]>? validationErrors = null)
    {
        return BadRequest(null, errorMessage, validationErrors);
    }

    /// <summary>
    /// Returns a 400 - bad request error with optional response object <typeparamref name="TResponse" />, error message and
    /// field specific validation errors
    /// </summary>
    /// <param name="response">The optional response object.</param>
    /// <param name="errorMessage">The optional error message.</param>
    /// <param name="validationErrors">The optional field-specific validation errors.</param>
    /// <returns>A <see cref="IHandlerResult" /> representing a 400 - bad request error.</returns>
    public IHandlerResult BadRequest(TResponse? response, string? errorMessage = null, IDictionary<string, string[]>? validationErrors = null)
    {
        return Error(CarrotStatusCode.BadRequest, response, errorMessage, validationErrors);
    }
}