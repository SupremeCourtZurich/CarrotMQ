using System.Threading;
using System.Threading.Tasks;

namespace CarrotMQ.Core;

/// <summary>
/// Used to alter the message and the <see cref="Context" /> before the message is sent.
/// Useful to set e.g. custom headers that must be set for every message.
/// </summary>
public interface IMessageEnricher
{
    /// <summary>
    /// Receives the message and context before the message is sent.
    /// </summary>
    public Task EnrichMessageAsync(object message, Context context, CancellationToken cancellationToken);
}