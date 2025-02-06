using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core;

/// <summary>
/// Represents the response of a call sent over <see cref="CarrotClient" />.
/// </summary>
public class CarrotResponse
{
    /// <summary>
    /// The status code. They are inlined to the typical http status codes (e.g. 500).
    /// Helper constants are defined in <see cref="CarrotStatusCode" />
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Detailed error object
    /// </summary>
    public CarrotError? Error { get; set; }

    /// <summary>
    /// The response of the request.
    /// </summary>
    public object? Content { get; set; }

    /// <summary>
    /// The original request which generated this response
    /// </summary>
    public object? Request { get; set; }
}

/// <summary>
/// Response of a call sent over <see cref="CarrotClient" /> with the response object <see cref="Content" /> wrapped
/// inside
/// </summary>
/// <typeparam name="TRequest">The type of the original request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public class CarrotResponse<TRequest, TResponse> : CarrotResponse, _IRequest<CarrotResponse<TRequest, TResponse>, NoResponse>
    where TRequest : _IRequest<TRequest, TResponse>
    where TResponse : class
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CarrotResponse{TRequest, TResponse}" /> class.
    /// </summary>
    public CarrotResponse()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CarrotResponse{TRequest, TResponse}" /> class with the specified response
    /// and original request.
    /// </summary>
    /// <param name="content">The response of the request.</param>
    /// <param name="request">The original request which generated this response.</param>
    public CarrotResponse(TResponse? content, TRequest? request)
    {
        Content = content;
        Request = request;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CarrotResponse{TRequest, TResponse}" /> class with the specified status
    /// code.
    /// </summary>
    /// <param name="statusCode">The status code of the response.</param>
    public CarrotResponse(int statusCode)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// The content of the response.
    /// </summary>
    public new TResponse? Content { get; set; }

    /// <summary>
    /// The original request which generated this response
    /// </summary>
    public new TRequest? Request { get; set; }
}