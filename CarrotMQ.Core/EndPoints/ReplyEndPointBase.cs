namespace CarrotMQ.Core.EndPoints;

/// <summary>
/// Represents base class for messaging endpoints used for the reply messages (reply of
/// <see cref="Dto.ICommand{TCommand,TResponse,TEndPointDefinition}" /> or
/// <see cref="Dto.IQuery{TQuery,TResponse,TEndPointDefinition}" />)
/// </summary>
public abstract class ReplyEndPointBase
{
    /// <summary>
    /// The pseudo-queue used as routing key for direct reply messages (channel reply)
    /// </summary>
    public const string ChannelOutRoutingKey = "amq.rabbitmq.reply-to";

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplyEndPointBase" /> class.
    /// </summary>
    /// <param name="exchangeName">The name of the messaging exchange to which the reply will be sent</param>
    /// <param name="routingKey">The routing key to use for the reply message</param>
    /// <param name="includeRequestPayloadInResponse">A flag indicating whether to include the request payload in the response.</param>
    protected ReplyEndPointBase(string exchangeName, string routingKey, bool includeRequestPayloadInResponse = false)
    {
        Exchange = exchangeName;
        RoutingKey = routingKey;
        IncludeRequestPayloadInResponse = includeRequestPayloadInResponse;
    }

    /// <summary>
    /// Name of the messaging exchange to which the reply will be sent
    /// </summary>
    public string Exchange { get; }

    /// <summary>
    /// Routing key used for the reply message
    /// </summary>
    public string RoutingKey { get; }

    /// <summary>
    /// Flag indicating whether to include the request payload in the response.
    /// </summary>
    public bool IncludeRequestPayloadInResponse { get; }
}