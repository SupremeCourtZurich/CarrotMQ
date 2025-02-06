using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarrotMQ.Core.Serialization;

/// <summary>
/// Default implementation of the <see cref="ICarrotSerializer" /> interface using the System.Text.Json serializer.
/// </summary>
internal sealed class DefaultCarrotSerializer : ICarrotSerializer
{
    /// <summary>
    /// Allow serialization and deserialization of <see cref="double.PositiveInfinity" />
    /// <see cref="double.NegativeInfinity" /> and <see cref="double.NaN" />
    /// </summary>
    private readonly JsonSerializerOptions _options =
        new(JsonSerializerOptions.Default) { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };

    /// <inheritdoc cref="ICarrotSerializer.Serialize{T}" />
    public string Serialize<T>(T obj) where T : notnull
    {
        // Serialize properties of derived classes? -> declare the object to be serialized as object
        // https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism?pivots=dotnet-6-0
        return JsonSerializer.Serialize<object>(obj, _options);
    }

    /// <inheritdoc cref="ICarrotSerializer.Deserialize{T}" />
    public T? Deserialize<T>(string dataString)
    {
        return JsonSerializer.Deserialize<T>(dataString, _options);
    }
}