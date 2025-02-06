using CarrotMQ.Core.MessageProcessing.Middleware;

namespace CarrotMQ.Core.Test.Helper;

public class TestMiddleware : ICarrotMiddleware
{
    public Task<bool> IsProcessed(Guid messageId)
    {
        return Task.FromResult(true);
    }

    public async Task InvokeAsync(MiddlewareContext context, Func<Task> nextAsync)
    {
        //My stuff

        await nextAsync().ConfigureAwait(false);

        //More stuff
    }
}