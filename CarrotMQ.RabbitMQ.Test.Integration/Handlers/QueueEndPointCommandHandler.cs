using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

namespace CarrotMQ.RabbitMQ.Test.Integration.Handlers;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class QueueEndPointCmdHandler : CommandHandlerBase<QueueEndPointCmd, QueueEndPointCmd.Response>
{
    private readonly ReceivedMessages _receivedMessages;

    public QueueEndPointCmdHandler(ReceivedMessages receivedMessages)
    {
        _receivedMessages = receivedMessages;
    }

    public override async Task<IHandlerResult> HandleAsync(
        QueueEndPointCmd cmd,
        ConsumerContext consumerContext,
        CancellationToken cancellationToken)
    {
        await _receivedMessages.WriteAsync(cmd.Id, cancellationToken).ConfigureAwait(false);

        if (cmd.TaskWaitDuration > TimeSpan.Zero)
        {
            try
            {
                for (var i = 0; i < cmd.WaitDurationCount; i++)
                {
                    Console.WriteLine($"Task delay {i} {cmd.TaskWaitDuration.Milliseconds}ms");
                    await Task.Delay(cmd.TaskWaitDuration, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                return Cancel();
            }
        }

        if (cmd.ThreadWaitDuration > TimeSpan.Zero)
        {
            for (var i = 0; i < cmd.WaitDurationCount; i++)
            {
                Console.WriteLine($"Thread sleep {i} {cmd.ThreadWaitDuration.Milliseconds}ms");
                Thread.Sleep(cmd.ThreadWaitDuration);
            }
        }

        if (cmd.DoRetry)
        {
            return Retry();
        }

        if (cmd.DoReject)
        {
            return Reject();
        }

        if (cmd.ThrowException)
        {
            throw new ArgumentException($"Exception for {cmd.Id}", nameof(cmd));
        }

        if (cmd.ReturnError)
        {
            return Error(new QueueEndPointCmd.Response { Id = cmd.Id }, $"Error for {cmd.Id}");
        }

        if (cmd.ReturnErrorWithValidationErrors)
        {
            Dictionary<string, string[]> validations = new() { { "Error1", ["Error1.Message1", "Error1.Message2"] } };

            return Error($"CustomError {cmd.Id}", new Dictionary<string, string[]>(validations));
        }

        if (cmd.ReturnCustomStatusCode)
        {
            return Error(999, new QueueEndPointCmd.Response { Id = cmd.Id });
        }

        if (cmd.BadRequest)
        {
            return BadRequest(new QueueEndPointCmd.Response { Id = cmd.Id }, "Validation error");
        }

        return Ok(new QueueEndPointCmd.Response { Id = cmd.Id });
    }
}