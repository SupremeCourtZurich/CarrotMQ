﻿using CarrotMQ.Core;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class QueueEndPointQueryResponseHandler : ResponseHandlerBase<QueueEndPointQuery, QueueEndPointQuery.Response>
{
    private readonly ReceivedResponses _receivedResponses;

    public QueueEndPointQueryResponseHandler(ReceivedResponses receivedResponses)
    {
        _receivedResponses = receivedResponses;
    }

    public override async Task<IHandlerResult> HandleAsync(
        CarrotResponse<QueueEndPointQuery, QueueEndPointQuery.Response> carrotResponse,
        ConsumerContext consumerContext,
        CancellationToken cancellationToken)
    {
        var response = new Response
        {
            Id = carrotResponse.Content?.Id ?? 0,
            StatusCode = carrotResponse.StatusCode,
            Error = carrotResponse.Error
        };
        await _receivedResponses.WriteAsync(response, cancellationToken).ConfigureAwait(false);

        return Ok();
    }
}