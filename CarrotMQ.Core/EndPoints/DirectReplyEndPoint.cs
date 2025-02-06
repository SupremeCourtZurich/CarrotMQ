namespace CarrotMQ.Core.EndPoints;

/// <summary>
/// Represents a direct reply messaging endpoint, derived from <see cref="ReplyEndPointBase" />.
/// It uses the pseudo-queue "amq.rabbitmq.reply-to" as routing key
/// </summary>
internal sealed class DirectReplyEndPoint : ReplyEndPointBase
{
    public DirectReplyEndPoint() : base(string.Empty, ChannelOutRoutingKey)
    {
    }
}