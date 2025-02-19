using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using Dto;
using Microsoft.Extensions.Logging;

namespace Service1;

public class MyCommandHandler : CommandHandlerBase<MyCommand, MyCommand.Response>
{
    private readonly ILogger<MyCommandHandler> _logger;
    public MyCommandHandler(ILogger<MyCommandHandler> logger)
    {
        _logger = logger;
    }
    public override async Task<IHandlerResult> HandleAsync(MyCommand message, ConsumerContext consumerContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Command received: {message.Message}");

        await Task.Delay(1000, cancellationToken);

        return Ok(new MyCommand.Response { ResponseMessage = "Response" });
    }
}