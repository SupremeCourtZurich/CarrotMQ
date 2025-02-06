using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class ExchangeEndPointGenericResponseCmdHandler : CommandHandlerBase<ExchangeEndPointGenericResponseCmd,
    ExchangeEndPointGenericResponseCmd.GenericResponse<ExchangeEndPointGenericResponseCmd.Response>>
{
    private readonly ReceivedMessages _receivedMessages;

    public ExchangeEndPointGenericResponseCmdHandler(ReceivedMessages receivedMessages)
    {
        _receivedMessages = receivedMessages;
    }

    public override async Task<IHandlerResult> HandleAsync(
        ExchangeEndPointGenericResponseCmd cmd,
        ConsumerContext consumerContext,
        CancellationToken cancellationToken)
    {
        await _receivedMessages.WriteAsync(cmd.Id, cancellationToken).ConfigureAwait(false);

        return Ok(
            new ExchangeEndPointGenericResponseCmd.GenericResponse<ExchangeEndPointGenericResponseCmd.Response>
            {
                InnerResponse = new ExchangeEndPointGenericResponseCmd.Response { Id = cmd.Id }
            });
    }
}