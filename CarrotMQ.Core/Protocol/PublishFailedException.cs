using System;

namespace CarrotMQ.Core.Protocol;

/// <summary>
/// Represents a publish that the broker did not confirm, with the <see cref="Reason" /> when it is known.
/// </summary>
public class PublishFailedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PublishFailedException" /> class.
    /// </summary>
    public PublishFailedException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishFailedException" /> class with a specific message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public PublishFailedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishFailedException" /> class with a specific message and an inner
    /// exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="inner">The inner exception that caused this exception.</param>
    public PublishFailedException(string message, Exception inner)
        : base(message, inner)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishFailedException" /> class with a reason.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="reason">Why the publish was not confirmed.</param>
    /// <param name="inner">The transport's exception that caused this exception, if any.</param>
    public PublishFailedException(string message, PublishFailureReason reason, Exception? inner = null)
        : base(message, inner)
    {
        Reason = reason;
    }

    /// <summary>
    /// Why the publish was not confirmed; <see cref="PublishFailureReason.Unknown" /> unless a constructor sets it.
    /// </summary>
    public PublishFailureReason Reason { get; } = PublishFailureReason.Unknown;
}