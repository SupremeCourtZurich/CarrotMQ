namespace CarrotMQ.Core.Protocol;

/// <summary>
/// Represents a message sent with CarrotMQ.
/// </summary>
public sealed class CarrotMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CarrotMessage" /> class with default values.
    /// </summary>
    public CarrotMessage()
    {
        Header = new CarrotHeader();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CarrotMessage" /> class with the specified header and message payload.
    /// </summary>
    /// <param name="header">The header associated with the message.</param>
    /// <param name="payload">The payload of the message.</param>
    public CarrotMessage(CarrotHeader header, string payload)
    {
        Header = header;
        Payload = payload;
    }

    /// <summary>
    /// Header of the message.
    /// </summary>
    public CarrotHeader Header { get; set; }

    /// <summary>
    /// Payload of the message.
    /// </summary>
    public string? Payload { get; set; }
}