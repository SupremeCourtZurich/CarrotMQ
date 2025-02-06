using System;
using System.Threading.Tasks;
using CarrotMQ.Core.Common;
using CarrotMQ.Core.Dto;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.MessageProcessing.Middleware;
using CarrotMQ.Core.Serialization;

namespace CarrotMQ.Core.MessageProcessing;

/// <summary>
/// Caller proxy which calls the RequestHandler transforming the object arguments into the generic argument types
/// (TRequest, TResponse)
/// </summary>
/// <typeparam name="TRequestHandler">The type of the request handler to be used.</typeparam>
/// <typeparam name="TRequest">The type of the request.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
/// <remarks>
/// The <see cref="RequestHandlerProcessor{TRequestHandler, TRequest, TResponse}" /> class is responsible for processing
/// requests (<see cref="ICommand{TCommand,TResponse,TEndPointDefinition}" /> or
/// <see cref="IQuery{TQuery,TResponse,TEndPointDefinition}" />).
/// It inherits from <see cref="HandlerProcessorBase" />.
/// </remarks>
internal sealed class RequestHandlerProcessor<TRequestHandler, TRequest, TResponse> : HandlerProcessorBase
    where TRequestHandler : RequestHandlerBase<TRequest, TResponse>
    where TRequest : class, _IRequest<TRequest, TResponse>
    where TResponse : class

{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequestHandlerProcessor{TRequestHandler, TRequest, TResponse}" /> class.
    /// </summary>
    internal RequestHandlerProcessor() : base(CalledMethodResolver.BuildCalledMethodKey(typeof(TRequest)))
    {
    }

    internal override Type MessageType => typeof(TRequest);

    internal override Type HandlerType => typeof(TRequestHandler);

    /// <inheritdoc />
    internal override async Task HandleAsync(MiddlewareContext middlewareContext, IDependencyInjector scopedDependencyInjector)
    {
        var carrotMessage = middlewareContext.Message;

        IHandlerResult result = new RejectResult();
        var serializer = scopedDependencyInjector.GetCarrotSerializer();

        try
        {
            var requestHandler = GetHandler<TRequestHandler, TRequest, TResponse>(scopedDependencyInjector);
            var request = serializer.DeserializeWithNullCheck<TRequest>(carrotMessage.Payload);
            result = await requestHandler.HandleAsync(request, middlewareContext.ConsumerContext, middlewareContext.CancellationToken)
                .ConfigureAwait(false);

            if (carrotMessage.Header.IncludeRequestPayloadInResponse)
            {
                result.Response.Request = request;
            }
        }
        catch
        {
            var errorMessage = $"Unhandled exception while handling message: {carrotMessage.Payload}";
            result = new RejectResult(new CarrotError(errorMessage));

            middlewareContext.IsErrorResult = true;

            throw;
        }
        finally
        {
            middlewareContext.HandlerResult = result;
            middlewareContext.DeliveryStatus = result.DeliveryStatus;
            middlewareContext.ResponseRequired = carrotMessage.HasReply() && result.DeliveryStatus != DeliveryStatus.Retry;
        }
    }
}