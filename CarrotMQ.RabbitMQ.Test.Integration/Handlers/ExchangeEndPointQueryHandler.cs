using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class ExchangeEndPointQueryHandler : QueryHandlerBase<ExchangeEndPointQuery, ExchangeEndPointQuery.Response>
{
    private readonly ReceivedMessages _receivedMessages;

    public ExchangeEndPointQueryHandler(ReceivedMessages receivedMessages)
    {
        _receivedMessages = receivedMessages;
    }

    public override async Task<IHandlerResult> HandleAsync(
        ExchangeEndPointQuery query,
        ConsumerContext consumerContext,
        CancellationToken cancellationToken)
    {
        await _receivedMessages.WriteAsync(query.Id, cancellationToken).ConfigureAwait(false);

        if (query.TaskWaitDuration > TimeSpan.Zero)
        {
            try
            {
                for (var i = 0; i < query.WaitDurationCount; i++)
                {
                    Console.WriteLine($"Task delay {i} {query.TaskWaitDuration.Milliseconds}ms");
                    await Task.Delay(query.TaskWaitDuration, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                return Cancel();
            }
        }

        if (query.ThreadWaitDuration > TimeSpan.Zero)
        {
            for (var i = 0; i < query.WaitDurationCount; i++)
            {
                Console.WriteLine($"Thread sleep {i} {query.TaskWaitDuration.Milliseconds}ms");
                Thread.Sleep(query.TaskWaitDuration);
            }
        }

        if (query.DoRetry)
        {
            return Retry();
        }

        if (query.DoReject)
        {
            return Reject();
        }

        if (query.ThrowException)
        {
            throw new ArgumentException($"Exception for {query.Id}", nameof(query));
        }

        if (query.ReturnError)
        {
            return Error(new ExchangeEndPointQuery.Response { Id = query.Id }, $"Error for {query.Id}");
        }

        if (query.ReturnErrorWithValidationErrors)
        {
            Dictionary<string, string[]> validations = new() { { "Error1", ["Error1.Message1", "Error1.Message2"] } };

            return Error($"CustomError {query.Id}", new Dictionary<string, string[]>(validations));
        }

        if (query.ReturnCustomStatusCode)
        {
            return Error(999, new ExchangeEndPointQuery.Response { Id = query.Id });
        }

        if (query.BadRequest)
        {
            return BadRequest(new ExchangeEndPointQuery.Response { Id = query.Id }, "Validation error");
        }

        return Ok(new ExchangeEndPointQuery.Response { Id = query.Id });
    }
}