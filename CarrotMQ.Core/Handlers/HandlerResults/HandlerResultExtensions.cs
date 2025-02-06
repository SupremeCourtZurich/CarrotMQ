using System.Threading.Tasks;

namespace CarrotMQ.Core.Handlers.HandlerResults;

/// <summary>
/// Extension methods for <see cref="IHandlerResult" />.
/// </summary>
public static class HandlerResultExtensions
{
    /// <summary>
    /// Converts the current <see cref="IHandlerResult" /> instance into a completed <see cref="Task{IHandlerResult}" />.
    /// </summary>
    /// <returns>A task representing the asynchronous completion of the handling result.</returns>
    public static Task<IHandlerResult> AsTask(this IHandlerResult handlerResult)
    {
        return Task.FromResult(handlerResult);
    }
}