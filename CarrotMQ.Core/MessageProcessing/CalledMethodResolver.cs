using System;

namespace CarrotMQ.Core.MessageProcessing;

/// <summary>
/// Provides methods for building "called method keys" used to determine which handler processes the message
/// </summary>
public static class CalledMethodResolver
{
    private const string ResponsePrefix = "Response:";

    /// <summary>
    /// Builds a called method handler key based on the type of the message
    /// </summary>
    /// <param name="type">The type of the message</param>
    /// <returns>The handler key.</returns>
    public static string BuildCalledMethodKey(Type type)
    {
        return type.FullName ?? string.Empty;
    }

    /// <summary>
    /// Builds a called method key for a response message based on the key of the request message
    /// </summary>
    /// <param name="requestKey">The request key for which to build the response key.</param>
    /// <returns>The response key built from the specified request key.</returns>
    public static string BuildResponseCalledMethodKey(string requestKey)
    {
        return $"{ResponsePrefix}{requestKey}";
    }

    /// <summary>
    /// Builds a called method handler key for a response message based on the type of the request message
    /// </summary>
    /// <param name="type">The type the request message</param>
    /// <returns>The response called method handler key.</returns>
    public static string BuildResponseCalledMethodKey(Type type)
    {
        return BuildResponseCalledMethodKey(BuildCalledMethodKey(type));
    }
}