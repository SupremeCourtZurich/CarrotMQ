using System.Diagnostics.CodeAnalysis;

namespace CarrotMQ.Core.Serialization;

/// <summary>
/// Extension methods for the <see cref="ICarrotSerializer" /> interface.
/// </summary>
public static class CarrotSerializerExtensions
{
    /// <summary>
    /// Deserializes the specified payload using the provided serializer and performs null checks.
    /// </summary>
    /// <typeparam name="T">The type into which the payload is deserialized.</typeparam>
    /// <param name="serializer">The CarrotMQ serializer.</param>
    /// <param name="payload">The payload to be deserialized.</param>
    /// <returns>The deserialized object of type <typeparamref name="T" />.</returns>
    /// <exception cref="CarrotSerializerException">
    /// Thrown when the payload is null, empty, or the deserialization process fails.
    /// </exception>
    [SuppressMessage("ReSharper", "RedundantSuppressNullableWarningExpression")]
    public static T DeserializeWithNullCheck<T>(this ICarrotSerializer serializer, string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new CarrotSerializerException(string.Empty, typeof(T));
        }

        return serializer.Deserialize<T>(payload!) ?? throw new CarrotSerializerException(payload!, typeof(T));
    }
}