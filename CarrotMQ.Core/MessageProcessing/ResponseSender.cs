using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.Core.MessageProcessing.Middleware;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;
using CarrotMQ.Core.Tracing;
using Microsoft.Extensions.Logging;

namespace CarrotMQ.Core.MessageProcessing;

/// <inheritdoc />
internal sealed class ResponseSender : IResponseSender
{
    private readonly ILogger<ResponseSender> _logger;
    private readonly ICarrotMetricsRecorder _metricsRecorder;
    private readonly ICarrotSerializer _serializer;
    private readonly ITransport _transport;

    ///
    public ResponseSender(ITransport transport, ICarrotSerializer serializer, ICarrotMetricsRecorder metricsRecorder, ILogger<ResponseSender> logger)
    {
        _transport = transport;
        _serializer = serializer;
        _metricsRecorder = metricsRecorder;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task TrySendResponseAsync(MiddlewareContext middlewareContext)
    {
        var requestMessage = middlewareContext.Message;
        var result = middlewareContext.HandlerResult;

        if (result is null) return;

        if (middlewareContext.ResponseRequired)
        {
            if (!middlewareContext.ResponseSent)
            {
                await SendResponse(requestMessage, result).ConfigureAwait(false);
            }
        }
        else if (result is ErrorResult)
        {
            LogUnsentErrorResponse(requestMessage, result);
        }
    }

    private async Task SendResponse(CarrotMessage requestMessage, IHandlerResult result)
    {
        var responseMessage = CreateResponseMessage(requestMessage, result);
        await _transport.SendAsync(responseMessage, CancellationToken.None).ConfigureAwait(false);

        _logger.LogDebug(
            "Published response {CalledMethod}, StatusCode:{StatusCode}",
            requestMessage.Header.CalledMethod,
            result.Response.StatusCode);
        _metricsRecorder.ResponsePublished(requestMessage.Header.CalledMethod, result.Response.StatusCode);
    }

    private CarrotMessage CreateResponseMessage(CarrotMessage requestMessage, IHandlerResult result)
    {
        var responseCarrotHeader = new CarrotHeader
        {
            Exchange = requestMessage.Header.ReplyExchange,
            RoutingKey = requestMessage.Header.ReplyRoutingKey,
            CalledMethod = CalledMethodResolver.BuildResponseCalledMethodKey(requestMessage.Header.CalledMethod),
            CustomHeader = requestMessage.Header.CustomHeader?.ToDictionary(
                entry => entry.Key,
                entry => entry.Value),
            CorrelationId = requestMessage.Header.CorrelationId,
            MessageProperties = requestMessage.Header.MessageProperties,
            MessageId = Guid.NewGuid(),
            InitialUserName = requestMessage.Header.InitialUserName,
            InitialServiceName = requestMessage.Header.InitialServiceName,
            IncludeRequestPayloadInResponse = requestMessage.Header.IncludeRequestPayloadInResponse
        };

        var serializedResponse = _serializer.Serialize(result.Response);

        return new CarrotMessage(responseCarrotHeader, serializedResponse);
    }

    private void LogUnsentErrorResponse(CarrotMessage requestMessage, IHandlerResult result)
    {
        string errorPayload = string.Empty;
        if (result.Response.Content is not null)
        {
            errorPayload = _serializer.Serialize(result.Response.Content);
        }

        _logger.LogWarning(
            "Error response without somewhere to send it Request:{Payload} Response:{ErrorPayload}",
            requestMessage.Payload,
            errorPayload);
    }
}