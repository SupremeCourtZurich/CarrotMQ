namespace CarrotMQ.Core.Protocol;

/// <summary>
/// Status codes used in <see cref="CarrotResponse" />.
/// </summary>
public static class CarrotStatusCode
{
    /// <summary>
    /// Success status response code
    /// </summary>
    public const int Ok = 200;

    /// <summary>
    /// The consumer cannot or will not process the message due to something that is perceived to be a publisher error.
    /// </summary>
    public const int BadRequest = 400;

    /// <summary>
    /// The client must authenticate itself to get the requested response.
    /// </summary>
    public const int Unauthorized = 401;

    /// <summary>
    /// The client does not have access rights to the content.
    /// </summary>
    public const int Forbidden = 403;

    /// <summary>
    /// The consumer encountered an unexpected condition.
    /// </summary>
    public const int InternalServerError = 500;

    /// <summary>
    /// The message could not be handled in the given time.
    /// </summary>
    public const int GatewayTimeout = 504;
}