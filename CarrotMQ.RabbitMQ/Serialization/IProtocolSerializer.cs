using CarrotMQ.Core.Protocol;
using RabbitMQ.Client;

namespace CarrotMQ.RabbitMQ.Serialization;

/// <summary>
/// Represents an interface for serializing and deserializing CarrotMQ messages.<br />
/// <remarks>
/// This is the core serializer of this library. It is used to serialize the part of the messages used for publishing,
/// routing and distributing the messages.
/// If implemented wrongly it can break everything. --> prefer using our default implementation:
/// <see cref="ProtocolSerializer" />
/// </remarks>
/// </summary>
public interface IProtocolSerializer
{
    /// <summary>
    /// Serializes a <see cref="CarrotMessage" />.
    /// </summary>
    /// <param name="carrotMessage">The CarrotMQ message to be serialized.</param>
    /// <param name="basicProperties">The basic properties (AMQP Headers) sent along the message.</param>
    /// <returns>A string representation of the serialized CarrotMQ message.</returns>
    string Serialize(CarrotMessage carrotMessage, BasicProperties basicProperties);

    /// <summary>
    /// Deserializes into a <see cref="CarrotMessage" />.
    /// </summary>
    /// <param name="json">The string representation to be deserialized.</param>
    /// <param name="basicProperties">The basic properties (AMQP Headers) received along the message.</param>
    /// <returns>The deserialized CarrotMQ message.</returns>
    CarrotMessage Deserialize(string json, IReadOnlyBasicProperties basicProperties);
}