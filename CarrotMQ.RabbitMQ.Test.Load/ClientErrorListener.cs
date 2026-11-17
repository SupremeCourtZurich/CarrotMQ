using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

namespace CarrotMQ.RabbitMQ.Test.Load;

/// <summary>
/// Records the errors RabbitMQ.Client reports through its EventSource, with their stack traces.
/// </summary>
internal sealed class ClientErrorListener : EventListener
{
    private readonly ConcurrentDictionary<string, int> _errors = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> Errors => _errors;

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name.Contains("rabbitmq", StringComparison.OrdinalIgnoreCase))
        {
            EnableEvents(eventSource, EventLevel.Error, EventKeywords.All);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.Level > EventLevel.Error || eventData.Payload == null) return;

        var text = string.Join(" | ", eventData.Payload.Select(Describe));
        _errors.AddOrUpdate(text, 1, (_, n) => n + 1);
    }

    private static string Describe(object? value)
    {
        return value switch
        {
            null => "null",
            IDictionary<string, object> fields => string.Join(", ", fields.Select(f => $"{f.Key}={f.Value}")),
            _ => value.ToString() ?? string.Empty
        };
    }
}
