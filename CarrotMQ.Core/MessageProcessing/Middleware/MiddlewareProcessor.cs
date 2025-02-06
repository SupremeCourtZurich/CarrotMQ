using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CarrotMQ.Core.MessageProcessing.Middleware;

/// <inheritdoc cref="IMiddlewareProcessor" />
internal sealed class MiddlewareProcessor : IMiddlewareProcessor
{
    private readonly IEnumerable<ICarrotMiddleware> _middlewareCollection;

    //NOTE: Services are always provided in order of registration:
    //https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#service-registration-methods
    /// <inheritdoc cref="IMiddlewareProcessor" />
    public MiddlewareProcessor(IEnumerable<ICarrotMiddleware> middlewareCollection)
    {
        _middlewareCollection = middlewareCollection;
    }

    /// <inheritdoc cref="IMiddlewareProcessor" />
    public async Task RunAsync(MiddlewareContext context, Func<Task> handlerAction)
    {
        var run = handlerAction;

        foreach (var carrotMiddleware in _middlewareCollection.Reverse())
        {
            var next = run;
            run = async () => await carrotMiddleware.InvokeAsync(context, next).ConfigureAwait(false);
        }

        await run.Invoke().ConfigureAwait(false);
    }
}