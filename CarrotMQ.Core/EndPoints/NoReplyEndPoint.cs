namespace CarrotMQ.Core.EndPoints;

internal sealed class NoReplyEndPoint : ReplyEndPointBase
{
    public NoReplyEndPoint() : base(string.Empty, string.Empty)
    {
    }
}