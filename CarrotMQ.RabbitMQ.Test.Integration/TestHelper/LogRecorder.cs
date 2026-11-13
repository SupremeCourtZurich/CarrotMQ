using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

/// <summary>
/// Records every log message of a host, so tests can wait for what the library logs.
/// </summary>
internal sealed class LogRecorder : ILoggerProvider
{
    private const string PublishRefused = "Publish refused:";
    private readonly ConcurrentQueue<string> _messages = new();

    /// <summary>
    /// The number of messages the recovery channel identified as refused.
    /// </summary>
    public int Refusals => _messages.Count(m => m.StartsWith(PublishRefused, StringComparison.Ordinal));

    public Task WaitForRefusalsAsync(int count)
    {
        return Wait.UntilAsync(() => Refusals >= count, $"{count} refusals logged");
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new Logger(_messages);
    }

    public void Dispose()
    {
    }

    private sealed class Logger : ILogger
    {
        private readonly ConcurrentQueue<string> _messages;

        public Logger(ConcurrentQueue<string> messages)
        {
            _messages = messages;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _messages.Enqueue(formatter(state, exception));
        }
    }
}