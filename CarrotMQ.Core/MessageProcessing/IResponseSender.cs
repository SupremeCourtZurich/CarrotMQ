using System.Threading.Tasks;
using CarrotMQ.Core.MessageProcessing.Middleware;

namespace CarrotMQ.Core.MessageProcessing;

/// <summary>
/// Used to send a response to the client
/// </summary>
public interface IResponseSender
{
    /// <summary>
    /// Sends the response based on the <see cref="MiddlewareContext" />
    /// </summary>
    /// <param name="middlewareContext"></param>
    Task TrySendResponseAsync(MiddlewareContext middlewareContext);
}