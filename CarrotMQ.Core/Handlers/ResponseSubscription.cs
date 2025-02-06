using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CarrotMQ.Core.Common;
using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.Dto.Internals;
using Microsoft.Extensions.Logging;

namespace CarrotMQ.Core.Handlers;

/// <summary>
/// Represents a subscription to a CarrotMQ response <see cref="CarrotResponse{TRequest,TResponse}" />
/// </summary>
/// <typeparam name="TRequest">The type of the original request that generated this response.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
/// <remarks>
/// You can register this subscription with <see cref="HandlerCollection.AddResponseSubscription{TRequest,TResponse}" /> when
/// configuring the DI.
/// </remarks>
public class ResponseSubscription<TRequest, TResponse> where TRequest : _IRequest<TRequest, TResponse> where TResponse : class
{
    private readonly ILogger<ResponseSubscription<TRequest, TResponse>> _logger;

    /// <summary>
    /// An event handler that is invoked when a CarrotMQ response of type <see cref="CarrotResponse{TRequest,TResponse}" />> is received.
    /// </summary>
    public AsyncEventHandler<ResponseSubscriptionEventArgs<TRequest, TResponse>>? ResponseReceived;

    /// 
    public ResponseSubscription(ILogger<ResponseSubscription<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    internal async Task OnResponseReceived(CarrotResponse<TRequest, TResponse> message, ConsumerContext consumerContext)
    {
        if (ResponseReceived is null)
        {
            return;
        }

        var handlerExceptions = new List<Exception>();

        foreach (var messageCallback in ResponseReceived.GetHandlers())
        {
            try
            {
                await messageCallback.Invoke(this, new ResponseSubscriptionEventArgs<TRequest, TResponse>(message, consumerContext))
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while handling event of type {ResponseType}", typeof(CarrotResponse<TRequest, TResponse>).FullName);
                handlerExceptions.Add(e);
            }
        }

        if (handlerExceptions.Count > 0)
        {
            throw new AggregateException("One or more callbacks threw exceptions", handlerExceptions);
        }
    }
}