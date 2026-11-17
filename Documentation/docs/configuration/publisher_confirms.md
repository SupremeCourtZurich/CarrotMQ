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
| `IsolateRefusedPublishes` | false | If true, a [refused publish](#refused-publishes) fails at once with its reason, and the other unconfirmed messages are republished on a separate channel. |
| `RecoveryChannelIdleTimeoutInMs` | 60000 | With `IsolateRefusedPublishes`: milliseconds without work after which the channel used for [refused publishes](#refused-publishes) is closed; also the longest wait there for a request's reply before the next message is published. |

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

`RetryLimitExceededException` is thrown when a message sent with publisher confirms enabled cannot be positively acknowledged by the broker: after `RetryLimit` attempts, or at once when the broker refuses the message.

### When It Is Thrown

1. A message is published with `MessageProperties.PublisherConfirm = true` (the default).
2. With `IsolateRefusedPublishes`, the broker refuses the publish (see [Refused Publishes](#refused-publishes)): the exception is raised to the caller at once.
3. Otherwise, the broker sends a NACK (negative acknowledgement) or the confirmation times out: CarrotMQ republishes the message and increments an internal retry counter.
4. Once the counter exceeds `PublisherConfirmOptions.RetryLimit`, the exception is raised to the caller.

### Why It Failed

`RetryLimitExceededException` derives from `PublishFailedException` (CarrotMQ.Core). Its `Reason` tells the cause; the broker's reply code and text are in the message, the RabbitMQ exception is the `InnerException`.

| `Reason` | Meaning | Typical reaction |
|---|---|---|
| `DestinationNotFound` | The exchange does not exist (404). Only with `IsolateRefusedPublishes`. | This message's own problem. |
| `AccessRefused` | Publishing to the exchange is not allowed, e.g. an internal exchange (403). Only with `IsolateRefusedPublishes`. | This message's own problem. |
| `MessageRejected` | The broker refused the message itself (406). Only with `IsolateRefusedPublishes`. | This message's own problem. |
| `Unreachable` | The connection to the broker was lost on the last attempt. | Back off and retry later. |
| `Unknown` | No confirm or a NACK, without a known cause. | Retry later. |

### While the Broker Is Unreachable

A publish waits for the connection to recover, whether or not the process has connected before. The wait ends:

| When | Exception | `Reason` |
|---|---|---|
| The message's TTL expires | `PublishFailedException` | `Unreachable` while the connection is down or the channel is still being reopened, otherwise the cause of the last attempt (usually `Unknown`) |
| `InitialConnectionTimeout` passes before the first connection | `PublishFailedException` | `Unreachable` |
| The caller's `CancellationToken` is cancelled | `OperationCanceledException` | The `InnerException` is the `PublishFailedException` with the reason |

Events have no TTL by default: without a TTL or a `CancellationToken`, a publish waits until the broker is back. A request whose publish was confirmed but whose reply did not arrive within the TTL ends with `OperationCanceledException`.

> [!NOTE]
> When `RetryLimitExceededException` is thrown, the message **may or may not** have been delivered — the exception only means that delivery could not be **confirmed**. If delivery guarantees are critical, implement an outbox pattern or idempotency in the consumer.

Handle this exception in publishing code to implement application-level fallback logic:

```csharp
try
{
    await carrotClient.PublishAsync(myEvent, messageProperties: MessageProperties.Default with { Ttl = 10_000 });
}
catch (PublishFailedException ex) when (ex.Reason is PublishFailureReason.DestinationNotFound
                                            or PublishFailureReason.AccessRefused
                                            or PublishFailureReason.MessageRejected)
{
    logger.LogError(ex, "The broker refused {Event}", myEvent);
    // Mark the message as failed, raise an alert, etc.
}
catch (PublishFailedException)
{
    // Broker unreachable or no confirm: keep the message and retry later.
}
```

### When Publisher Confirms Are Disabled

If you disable publisher confirms via `MessageProperties { PublisherConfirm = false }`, `RetryLimitExceededException` is never thrown for that message. The broker does not acknowledge the send, and CarrotMQ returns immediately after dispatching.

---

## Refused Publishes

When the broker refuses a publish, for example because the exchange does not exist (404), it closes the channel and discards every message published after it.

- **`IsolateRefusedPublishes = false` (default):** CarrotMQ reopens the channel and republishes all unconfirmed messages, the refused one included. Each attempt of the refused message closes the channel again and discards the messages published after it, so they are retried with it and can also fail with `RetryLimitExceededException` (`Reason = Unknown`) once they exceed `RetryLimit`. The refused message fails with `RetryLimitExceededException` and `Reason = Unknown` after `RetryLimit` retries. An exchange declared in the meantime lets it through.
- **`IsolateRefusedPublishes = true`:** CarrotMQ republishes the unconfirmed messages one at a time on a separate channel. The refused message fails at once with `RetryLimitExceededException` and the matching `Reason`; it is not retried, so an exchange declared later does not let it through. The other messages are confirmed as usual. After a request is confirmed there, the next message waits for its reply, at most until the request times out or `RecoveryChannelIdleTimeoutInMs` passes.

Either way, declare exchanges before publishing to them.

---

## Throughput Considerations

- **`MaxConcurrentConfirms`** acts as a back-pressure mechanism. Lower values reduce memory usage; higher values allow more parallelism but consume more resources.
- **`RetryIntervalInMs`** combined with **`RetryLimit`** determines the maximum time CarrotMQ will spend attempting to deliver a single message: `RetryLimit × RetryIntervalInMs` ms.
- For high-throughput scenarios, consider increasing `MaxConcurrentConfirms` and reducing `RetryIntervalInMs` to keep the pipeline moving.
