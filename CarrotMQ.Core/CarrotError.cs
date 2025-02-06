using System.Collections.Generic;

namespace CarrotMQ.Core;

/// <summary>
/// Represents an error in the CarrotMQ messaging system.
/// </summary>
public sealed class CarrotError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CarrotError" /> class.
    /// </summary>
    public CarrotError()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CarrotError" /> class with the specified message and optional
    /// field-specific errors.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="errors">
    /// Field-specific error messages where the key is the request field path and the value is an array of
    /// errors for the given field.
    /// </param>
    public CarrotError(string message, IDictionary<string, string[]>? errors = null)
    {
        Message = message;
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    /// <summary>
    /// Error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Field specific error messages
    /// - Key = Request field path
    /// - Value = array of errors for the given field
    /// </summary>
    public IDictionary<string, string[]> Errors { get; set; } = new Dictionary<string, string[]>();
}