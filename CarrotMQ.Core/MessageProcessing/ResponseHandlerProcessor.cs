using System;
using System.Threading.Tasks;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.MessageProcessing.Middleware;
using CarrotMQ.Core.Serialization;

namespace CarrotMQ.Core.MessageProcessing;

/// <summary>
/// Caller proxy which calls the ResponseHandler transforming the object arguments into the generic argument types
/// (TRequest, TResponse)
/// </summary>
/// <typeparam name="TResponseHandler">The type of the response handler to be used.</typeparam>
/// <typeparam name="TRequest">The type of the request associated with the response.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
/// <remarks>
/// The <see cref="ResponseHandlerProcessor{TResponseHandler, TRequest, TResponse}" /> class is responsible for processing
/// responses.
/// It inherits from <see cref="HandlerProcessorBase" />.
/// </remarks>
internal sealed class ResponseHandlerProcessor<TResponseHandler, TRequest, TResponse> : HandlerProcessorBase
    where TResponseHandler : ResponseHandlerBase<TRequest, TResponse>
    where TResponse : class
    where TRequest : class, _IRequest<TRequest, TResponse>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseHandlerProcessor{TResponseHandler, TRequest, TResponse}" /> class.
    /// </summary>
    internal ResponseHandlerProcessor() :
        base(CalledMethodResolver.BuildResponseCalledMethodKey(typeof(TRequest)))
    {
    }

    internal override Type MessageType => typeof(TRequest);

    internal override Type HandlerType => typeof(TResponseHandler);

    /// <inheritdoc />
    internal override async Task HandleAsync(MiddlewareContext middlewareContext, IDependencyInjector scopedDependencyInjector)
    {
        var responseHandler = GetHandler<TResponseHandler, CarrotResponse<TRequest, TResponse>, NoResponse>(scopedDependencyInjector);

        var serializer = scopedDependencyInjector.GetCarrotSerializer();
        var response = serializer.DeserializeWithNullCheck<CarrotResponse<TRequest, TResponse>>(middlewareContext.Message.Payload);

        var result = await responseHandler.HandleAsync(response, middlewareContext.ConsumerContext, middlewareContext.CancellationToken)
            .ConfigureAwait(false);

        middlewareContext.DeliveryStatus = result.DeliveryStatus;
    }
}