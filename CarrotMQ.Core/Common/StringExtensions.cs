namespace CarrotMQ.Core.Common;

/// <summary>
/// Extension methods for the <see cref="string" /> type.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Truncates the specified string to the specified maximum length.
    /// </summary>
    /// <param name="value">The string to truncate.</param>
    /// <param name="maxLength">The maximum length of the truncated string.</param>
    /// <returns>
    /// A truncated string if the length exceeds the specified maximum length; otherwise, the original string.
    /// </returns>
    public static string? Truncate(this string? value, int maxLength)
    {
        return value?.Length > maxLength ? value.Substring(0, maxLength) : value;
    }
}