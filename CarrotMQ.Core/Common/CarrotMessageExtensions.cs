using CarrotMQ.Core.Protocol;

namespace CarrotMQ.Core.Common;

/// <summary>
/// Extension methods for CarrotMessage
/// </summary>
public static class CarrotMessageExtensions
{
    /// <summary>
    /// Does this <paramref name="message" /> expect a response
    /// </summary>
    public static bool HasReply(this CarrotMessage message)
    {
        return !(string.IsNullOrWhiteSpace(message.Header.ReplyExchange)
            && string.IsNullOrWhiteSpace(message.Header.ReplyRoutingKey));
    }
}