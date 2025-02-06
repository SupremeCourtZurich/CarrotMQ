using System;

namespace CarrotMQ.Core.Common;

/// <inheritdoc cref="IDateTimeProvider" />
public sealed class DateTimeProvider : IDateTimeProvider
{
    /// <inheritdoc />
    public DateTimeOffset Now => DateTimeOffset.Now;

    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}