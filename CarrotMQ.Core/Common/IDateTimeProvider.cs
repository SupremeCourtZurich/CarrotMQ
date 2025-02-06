using System;

namespace CarrotMQ.Core.Common;

/// <summary>
/// Defines an interface for providing current date and time information.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// Gets the current local date and time.
    /// </summary>
    /// <returns>A <see cref="DateTimeOffset" /> object representing the current local date and time.</returns>
    public DateTimeOffset Now { get; }

    /// <summary>
    /// Gets the current Coordinated Universal Time (UTC) date and time.
    /// </summary>
    /// <returns>A <see cref="DateTimeOffset" /> object representing the current UTC date and time.</returns>
    public DateTimeOffset UtcNow { get; }
}