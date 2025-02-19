using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using Dto;
using Microsoft.Extensions.Logging;

namespace Service;

#region MyEventHandlerDefinition
public class MyEventHandler : EventHandlerBase<MyEvent>
{
    private readonly ILogger<MyEventHandler> _logger;

    public MyEventHandler(ILogger<MyEventHandler> logger)
    {
        _logger = logger;
    }

    public override async Task<IHandlerResult> HandleAsync(MyEvent message, ConsumerContext consumerContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation(message.Message);

        await Task.Delay(100, cancellationToken).ConfigureAwait(false); // Do something with the event 

        return Ok();
    }
}
#endregion