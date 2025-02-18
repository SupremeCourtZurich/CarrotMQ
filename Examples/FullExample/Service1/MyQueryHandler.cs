using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using Dto;
using Microsoft.Extensions.Logging;

namespace Service1;

public class MyQueryHandler : QueryHandlerBase<MyQuery, MyQuery.Response>
{
    private readonly ILogger<MyQueryHandler> _logger;

    public MyQueryHandler(ILogger<MyQueryHandler> logger)
    {
        _logger = logger;
    }

    public override async Task<IHandlerResult> HandleAsync(MyQuery message, ConsumerContext consumerContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Query received: {message.Message}");

        await Task.Delay(1000, cancellationToken);

        return Ok(new MyQuery.Response { ResponseMessage = $"Response for '{message.Message}'" });
    }
}