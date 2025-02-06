using System;
using System.Threading.Tasks;

namespace CarrotMQ.Core.MessageProcessing.Middleware;

/// <summary>
/// Runs all registered <see cref="ICarrotMiddleware" />s.
/// </summary>
public interface IMiddlewareProcessor
{
    /// <summary>
    /// Runs the handler and all middleware
    /// </summary>
    Task RunAsync(MiddlewareContext context, Func<Task> handlerAction);
}