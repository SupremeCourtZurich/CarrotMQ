using CarrotMQ.Core.Protocol;
using RabbitMQ.Client;

namespace CarrotMQ.RabbitMQ.Serialization;

/// <summary>
/// Mapps <see cref="BasicProperties"/> from and to <see cref="CarrotMessage"/>.
/// </summary>
public interface IBasicPropertiesMapper
{
    /// <summary>
    /// Maps the basic properties to the CarrotMessage. (Carrot message has been deserialized from the payload of the message beforehand)
    /// </summary>
    void MapToMessage(IReadOnlyBasicProperties basicProperties, CarrotMessage carrotMessage);

    /// <summary>
    /// Create and set the basic properties before publishing the message
    /// </summary>
    BasicProperties CreateBasicProperties(CarrotHeader carrotHeader);
}