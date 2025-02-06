using Microsoft.Extensions.Logging;

namespace CarrotMQ.RabbitMQ.Test.Helper;

public static class TestLoggerFactory
{
    static TestLoggerFactory()
    {
        Instance = LoggerFactory.Create(
            builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddConsole();
            });
    }

    public static ILoggerFactory Instance { get; set; }

    public static ILogger<T> CreateLogger<T>()
    {
        return Instance.CreateLogger<T>();
    }
}