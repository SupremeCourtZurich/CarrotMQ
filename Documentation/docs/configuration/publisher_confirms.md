# Publisher Confirms

## What Are Publisher Confirms?

Publisher confirms are a RabbitMQ mechanism where the broker sends an acknowledgment back to the publisher after it has received and persisted a message. This provides **at-least-once delivery** guarantees from the publisher to the broker.

CarrotMQ enables publisher confirms **by default** (`MessageProperties.PublisherConfirm = true`). Every `PublishAsync` call will wait for the broker to confirm receipt before returning, unless explicitly disabled.

---

## PublisherConfirmOptions

Publisher confirm behaviour is controlled through `PublisherConfirmOptions`, nested under `BrokerConnectionOptions.PublisherConfirm`.

| Property | Default | Description |
|---|---|---|
| `MaxConcurrentConfirms` | 500 | Maximum number of messages that can be awaiting a broker confirm at any one time. Publishing blocks when this limit is reached. |
| `RetryLimit` | 100 | How many times to retry publishing a message if no positive ack is received from the broker. |
| `RetryIntervalInMs` | 1000 | Milliseconds to wait between retry attempts. |
| `RepublishEvaluationIntervalInMs` | 100 | How often (in milliseconds) CarrotMQ checks internally for messages that need to be retried. |

---

## Configuration

### Via Code

```csharp
builder.ConfigureBrokerConnection(options =>
{
    options.PublisherConfirm.MaxConcurrentConfirms = 200;
    options.PublisherConfirm.RetryLimit = 50;
    options.PublisherConfirm.RetryIntervalInMs = 2000;
});
```

### Via appsettings.json

```json
{
  "BrokerConnection": {
    "PublisherConfirm": {
      "MaxConcurrentConfirms": 200,
      "RetryLimit": 50,
      "RetryIntervalInMs": 2000
    }
  }
}
```

See [AppSettings Reference](../advanced/appsettings.md) for the full list of `BrokerConnection` settings.

---

## Disabling Publisher Confirms Per-Message

Publisher confirms can be disabled on a per-message basis by passing a custom `MessageProperties` instance:

```csharp
await carrotClient.PublishAsync(
    myEvent,
    messageProperties: new MessageProperties { PublisherConfirm = false });
```

> **Warning**: Disabling publisher confirms means the broker will not acknowledge receipt of the message. The message may be lost if the broker restarts or the connection drops immediately after publishing.

---

## RetryLimitExceededException

`RetryLimitExceededException` is thrown when a message sent with publisher confirms enabled cannot be positively acknowledged by the broker after `RetryLimit` attempts.

### When It Is Thrown

The exception occurs in the following sequence:

1. A message is published with `MessageProperties.PublisherConfirm = true` (the default).
2. The broker sends a NACK (negative acknowledgement) or the confirmation times out.
3. CarrotMQ enqueues the message for republishing and increments an internal retry counter.
4. Once the counter exceeds `PublisherConfirmOptions.RetryLimit`, the exception is raised to the caller.

> [!NOTE]
> When `RetryLimitExceededException` is thrown, the message **may or may not** have been delivered — the exception only means that delivery could not be **confirmed**. If delivery guarantees are critical, implement an outbox pattern or idempotency in the consumer.

Handle this exception in publishing code to implement application-level fallback logic:

```csharp
try
{
    await carrotClient.PublishAsync(myEvent);
}
catch (RetryLimitExceededException ex)
{
    logger.LogError(ex, "Failed to publish {Event} after retries", myEvent);
    // Persist to outbox, raise alert, etc.
}
```

### When Publisher Confirms Are Disabled

If you disable publisher confirms via `MessageProperties { PublisherConfirm = false }`, `RetryLimitExceededException` is never thrown for that message. The broker does not acknowledge the send, and CarrotMQ returns immediately after dispatching.

---

## Throughput Considerations

- **`MaxConcurrentConfirms`** acts as a back-pressure mechanism. Lower values reduce memory usage; higher values allow more parallelism but consume more resources.
- **`RetryIntervalInMs`** combined with **`RetryLimit`** determines the maximum time CarrotMQ will spend attempting to deliver a single message: `RetryLimit × RetryIntervalInMs` ms.
- For high-throughput scenarios, consider increasing `MaxConcurrentConfirms` and reducing `RetryIntervalInMs` to keep the pipeline moving.
