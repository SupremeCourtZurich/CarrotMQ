using System;

namespace CarrotMQ.Core.Serialization;

/// <summary>
/// Represents an exception thrown when deserialization of a payload into a specified target type fails.
/// </summary>
public class CarrotSerializerException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CarrotSerializerException" /> class.
    /// </summary>
    /// <param name="payload">The payload that could not be deserialized.</param>
    /// <param name="targetType">The target type into which the payload was intended to be deserialized.</param>
    public CarrotSerializerException(string payload, Type targetType)
        : base($"Payload could not be deserialized into type {targetType.FullName}")
    {
        Payload = payload;
        TargetType = targetType;
    }

    /// <summary>
    /// Payload that could not be deserialized.
    /// </summary>
    public string Payload { get; }

    /// <summary>
    /// Target type into which the payload was intended to be deserialized.
    /// </summary>
    public Type TargetType { get; }
}