using System;

namespace CarrotMQ.Core.MessageProcessing;

/// <summary>
/// Exception thrown when a handler cannot be registered due to a generic message type.
/// </summary>
public sealed class GenericMessageTypeException : Exception
{
    /// <summary>
    /// Private constructor for the exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    private GenericMessageTypeException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="GenericMessageTypeException" /> with a specific response type and handler type.
    /// </summary>
    /// <param name="response">The type of the response associated with the handler.</param>
    /// <param name="handlerType">The type of the handler class.</param>
    /// <returns>A new instance of <see cref="GenericMessageTypeException" />.</returns>
    public static GenericMessageTypeException Create(Type response, Type handlerType)
    {
        return new GenericMessageTypeException(GetErrorMessage(response, handlerType));
    }

    /// <summary>
    /// Generates an error message for a <see cref="GenericMessageTypeException" />.
    /// </summary>
    /// <param name="handlerType">The type of the handler class.</param>
    /// <param name="requestType">The type of the request associated with the handler.</param>
    /// <returns>An error message for a <see cref="GenericMessageTypeException" />.</returns>
    private static string GetErrorMessage(Type handlerType, Type requestType)
    {
        // Generate an error message describing the issue with generic message types.
        return
            $"The handler of type {handlerType.Name} ({handlerType.FullName}) could not be registered because the message type {requestType.Name} ({requestType.FullName}) is a generic type and therefore the handlerKey contains assembly related information.";
    }
}