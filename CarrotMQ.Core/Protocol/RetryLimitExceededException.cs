using System;

namespace CarrotMQ.Core.Protocol;

/// <summary>
/// Represents an exception thrown when the retry limit for a message sent with publisher confirm is exceeded.
/// No ack has been received from the broker to confirm the arrival of this message.
/// </summary>
public sealed class RetryLimitExceededException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryLimitExceededException" /> class with no specific message.
    /// </summary>
    public RetryLimitExceededException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryLimitExceededException" /> class with a specific message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public RetryLimitExceededException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryLimitExceededException" /> class with a specific message and an inner
    /// exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="inner">The inner exception that caused this exception.</param>
    public RetryLimitExceededException(string message, Exception inner)
        : base(message, inner)
    {
    }
}