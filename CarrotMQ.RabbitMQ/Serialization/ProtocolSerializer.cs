using System.Text.Json;
using System.Text.Json.Serialization;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;

namespace CarrotMQ.RabbitMQ.Serialization;

/// <summary>
/// Provides our implementation for serializing and deserializing CarrotMQ messages into JSON.
/// <remarks>We use the .NET System.Text.Json serializer to avoid clashes with a specific serializer.</remarks>
/// </summary>
public sealed class ProtocolSerializer : IProtocolSerializer
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    /// <inheritdoc cref="IProtocolSerializer.Serialize" />
    public string Serialize(CarrotMessage carrotMessage)
    {
        return JsonSerializer.Serialize(carrotMessage, JsonSerializerOptions);
    }

    /// <inheritdoc cref="IProtocolSerializer.Deserialize" />
    public CarrotMessage Deserialize(string json)
    {
        return DeserializeInternal(json) ?? throw new CarrotSerializerException(json, typeof(CarrotMessage));
    }

    private static CarrotMessage? DeserializeInternal(string json)
    {
        return JsonSerializer.Deserialize<CarrotMessage>(json, JsonSerializerOptions);
    }
}