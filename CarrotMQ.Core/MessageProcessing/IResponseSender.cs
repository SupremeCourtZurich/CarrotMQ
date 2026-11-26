using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.MessageProcessing.Middleware;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core.MessageProcessing;

/// <summary>
/// Used to send a response to the client
/// </summary>
public interface IResponseSender
{
    /// <summary>
    /// Sends the response based on the <see cref="MiddlewareContext" />, when one is required and not yet sent.
    /// </summary>
    /// <param name="middlewareContext">The context of the handled request.</param>
    /// <param name="cancellationToken">
    /// Bounds the publish of the response; when it is cancelled first, the response is dropped with a warning.
    /// </param>
    /// <exception cref="PublishFailedException">Thrown when the response could not be published for another reason.</exception>
    Task TrySendResponseAsync(MiddlewareContext middlewareContext, CancellationToken cancellationToken);
}
