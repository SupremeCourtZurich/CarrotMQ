using System;
using CarrotMQ.Core.Dto.Internals;

namespace CarrotMQ.Core.Handlers;

/// <summary>
/// Provides data for the <see cref="ResponseSubscription{TRequest,TResponse}.ResponseReceived" /> event.
/// </summary>
/// <typeparam name="TRequest">The type of the original request that generated this response.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public class ResponseSubscriptionEventArgs<TRequest, TResponse> : EventArgs where TRequest : _IRequest<TRequest, TResponse> where TResponse : class
{
    ///
    public ResponseSubscriptionEventArgs(CarrotResponse<TRequest, TResponse> response, ConsumerContext consumerContext)
    {
        Response = response;
        ConsumerContext = consumerContext;
    }

    /// <summary>
    /// CarrotMQ response message
    /// </summary>
    public CarrotResponse<TRequest, TResponse> Response { get; }

    ///
    public ConsumerContext ConsumerContext { get; }
}