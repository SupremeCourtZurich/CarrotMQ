using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarrotMQ.Core;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using Dto;
using Microsoft.Extensions.Logging;

namespace Service2;
internal class MyCommandResponseHandler : ResponseHandlerBase<MyCommand, MyCommand.Response>
{
    private readonly ILogger<MyCommandResponseHandler> _logger;

    public MyCommandResponseHandler(ILogger<MyCommandResponseHandler> logger)
    {
        _logger = logger;
    }

    public override async Task<IHandlerResult> HandleAsync(CarrotResponse<MyCommand, MyCommand.Response> message, ConsumerContext consumerContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Response received: {message.Content?.ResponseMessage}");

        await Task.Delay(1000, cancellationToken);
        
        return Ok();
    }
}
