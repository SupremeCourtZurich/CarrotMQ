using CarrotMQ.Core;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class ExchangeEndPointQueryResponseHandler : ResponseHandlerBase<ExchangeEndPointQuery, ExchangeEndPointQuery.Response>
{
    private readonly ReceivedResponses _receivedResponses;

    public ExchangeEndPointQueryResponseHandler(ReceivedResponses receivedResponses)
    {
        _receivedResponses = receivedResponses;
    }

    public override async Task<IHandlerResult> HandleAsync(
        CarrotResponse<ExchangeEndPointQuery, ExchangeEndPointQuery.Response> carrotResponse,
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