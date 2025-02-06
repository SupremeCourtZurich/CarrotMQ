using System;
using System.Threading.Tasks;

namespace CarrotMQ.Core.MessageProcessing.Middleware;

/// <summary>
/// Defines a middleware for the message processing pipeline"/>
/// </summary>
public interface ICarrotMiddleware
{
    /// <summary>
    /// Entry point for the middleware.
    /// The middleware must call the next delegate to proceed to the next middleware.<br />
    /// Example:<br /><br />
    /// <code>public async Task InvokeAsync(MiddlewareContext context, Func&lt;Task&gt; next)
    /// {
    ///     //Do preprocessing
    /// 
    ///     //The next middleware or the handler is called 
    ///     await next();
    /// 
    ///     //Do postprocessing
    /// }
    /// </code>
    /// </summary>
    /// <param name="context"></param>
    /// <param name="nextAsync"></param>
    Task InvokeAsync(MiddlewareContext context, Func<Task> nextAsync);
}