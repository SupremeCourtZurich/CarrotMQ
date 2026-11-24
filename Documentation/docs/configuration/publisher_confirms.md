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

### Why It Failed

`RetryLimitExceededException` derives from `PublishFailedException` (CarrotMQ.Core). Its `Reason` tells the cause of the last attempt; the RabbitMQ exception, if any, is the `InnerException`.

| `Reason` | Meaning | Typical reaction |
|---|---|---|
| `Unreachable` | The connection to the broker was lost on the last attempt. | Back off and retry later. |
| `Unknown` | No confirm or a NACK, without a known cause. | Retry later. |

When the broker refuses a publish, for example because the exchange does not exist (404), it closes the channel. CarrotMQ reopens it and republishes all unconfirmed messages, so the refused message fails with `Reason = Unknown` after `RetryLimit` retries. Declare exchanges before publishing to them.

### While the Broker Is Unreachable

A publish waits for the connection to recover, whether or not the process has connected before. The wait ends:

| When | Exception | `Reason` |
|---|---|---|
| The message's TTL expires | `OperationCanceledException` | The `InnerException` is a `PublishFailedException`: `Unreachable` while the connection is down or the channel is still being reopened, otherwise the cause of the last attempt (usually `Unknown`) |
| `InitialConnectionTimeout` passes before the first connection | `PublishFailedException` | `Unreachable` |
| The caller's `CancellationToken` is cancelled | `OperationCanceledException` | Same as for the TTL |

Events have no TTL by default: without a TTL or a `CancellationToken`, a publish waits until the broker is back. A request waits for its reply at most for its TTL, or, without a positive TTL, until its `CancellationToken` is cancelled (at most 5 seconds when it has none); a reply that does not arrive in time also ends with `OperationCanceledException`, without an inner `PublishFailedException`.

Handle these exceptions in publishing code to implement application-level fallback logic:

```csharp
try
{
    await carrotClient.PublishAsync(myEvent, messageProperties: MessageProperties.Default with { Ttl = 10_000 });
}
catch (OperationCanceledException ex) when (ex.InnerException is PublishFailedException { Reason: PublishFailureReason.Unreachable })
{
    // The TTL expired while the broker was unreachable: keep the message in the outbox and back off.
}
catch (PublishFailedException ex) when (ex.Reason == PublishFailureReason.Unreachable)
{
    // No connection within InitialConnectionTimeout, or the retries ran out on a lost connection: back off.
}
catch (Exception ex) when (ex is OperationCanceledException or PublishFailedException)
{
    logger.LogError(ex, "Failed to publish {Event}", myEvent);
    // No confirm: keep the message and retry later, raise an alert, etc.
}
```

### When Publisher Confirms Are Disabled

If you disable publisher confirms via `MessageProperties { PublisherConfirm = false }`, `RetryLimitExceededException` is never thrown for that message. The broker does not acknowledge the send, and CarrotMQ returns once the message is sent.

---

## Throughput Considerations

- **`MaxConcurrentConfirms`** acts as a back-pressure mechanism. Lower values reduce memory usage; higher values allow more parallelism but consume more resources.
- **`RetryIntervalInMs`** combined with **`RetryLimit`** determines the maximum time CarrotMQ will spend attempting to deliver a single message: `RetryLimit × RetryIntervalInMs` ms.
- For high-throughput scenarios, consider increasing `MaxConcurrentConfirms` and reducing `RetryIntervalInMs` to keep the pipeline moving.
