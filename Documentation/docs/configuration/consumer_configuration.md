# Consumer Configuration

Consumer behaviour is configured via `ConsumerBuilder`, which is accessible when registering a consumer on a queue builder.

## Registering a Consumer

Call `.WithConsumer()` or `.WithConsumer(configure => ...)` on any queue builder:

```csharp
builder.Queues.AddQuorum<MyQueue>()
    .WithConsumer(c => c
        .WithSingleAck()
        .WithPrefetchCount(10)
        .WithMessageProcessingTimeout(TimeSpan.FromSeconds(30)));
```

Calling `.WithConsumer()` with no arguments registers a consumer with default settings.

---

## Acknowledgment Modes

CarrotMQ supports three acknowledgment strategies. Choose the one that best matches your throughput and safety requirements.

| Method | AckCount | Behavior |
|---|---|---|
| `.WithAutoAck()` | 0 | RabbitMQ auto-acknowledges on delivery — messages are never redelivered. Highest throughput, lowest safety. |
| `.WithSingleAck()` | 1 | One message is acknowledged at a time. Safest option; recommended for most production use cases. |
| `.WithAckCount(n)` | n | Batch acknowledgment: sends a single ack for every *n* messages processed. Balances throughput and safety. |

### Trade-offs

- **Auto-ack** removes the overhead of explicit acknowledgment entirely, but any in-flight message is lost if the consumer crashes before processing completes. Additionally, `Retry()` and `Reject()` handler results are **silently ignored** in auto-ack mode (CarrotMQ logs a warning for `Reject()` and an error for `Retry()`). Dead-lettering does not apply.
- **Single-ack** gives the broker immediate feedback per message, making it easy to reason about redelivery behaviour and dead-lettering.
- **Batch ack** reduces the number of round-trips to the broker. If the consumer crashes before the batch is complete, all unacknowledged messages in that batch will be redelivered.

> [!WARNING]
> If `PrefetchCount > 0`, then `AckCount` must be less than or equal to `PrefetchCount`. CarrotMQ validates this at startup and throws an `OptionsValidationException` if the constraint is violated. For example, setting `WithAckCount(20)` with `WithPrefetchCount(10)` is invalid.

---

## PrefetchCount

`.WithPrefetchCount(ushort count)`

Controls how many unacknowledged messages RabbitMQ will deliver to this consumer at once (the AMQP *basic.qos* prefetch count).

- **Default**: `4` (hardcoded in `ConsumerConfiguration`).
- **Lower values** reduce memory pressure and limit the blast radius of a crash (fewer in-flight messages).
- **Higher values** increase throughput by keeping the consumer pipeline saturated.

### Relationship with `ConsumerDispatchConcurrency`

`PrefetchCount` and `ConsumerDispatchConcurrency` (set on `BrokerConnectionOptions`) work together to control throughput:

- **`ConsumerDispatchConcurrency`** controls how many dispatch threads the RabbitMQ client uses to deliver messages concurrently across all consumers on the connection. Setting it above `1` is what enables parallel message processing.
- **`PrefetchCount`** controls how many unacknowledged messages RabbitMQ will push to this consumer at once. It acts as a buffer: a higher value keeps the dispatch threads fed, preventing them from idling while waiting for the next delivery.

As a starting rule of thumb, set `PrefetchCount` to at least `ConsumerDispatchConcurrency` so there is always work available for every dispatch thread. For single-ack consumers with fast handlers, a 2× or 4× multiplier is common. For slow or I/O-heavy handlers, keep it closer to `1` to avoid hoarding messages that could be processed by other instances.

```csharp
.WithPrefetchCount(50)
```

> **Tip**: For single-ack consumers processing expensive operations, a prefetch count between 1 and 20 is usually a good starting point. For high-throughput pipelines with fast handlers, higher values (100+) may improve performance.

---

## MessageProcessingTimeout

`.WithMessageProcessingTimeout(TimeSpan timeout)`

Sets the maximum time a handler is allowed to run before processing is cancelled via the `CancellationToken` passed to `HandleAsync`.

- **Default:** `TimeSpan.FromMinutes(5)`.
- If the timeout elapses, the `CancellationToken` is cancelled and the message is **negatively acknowledged**.
- Whether the message is redelivered or dead-lettered depends on your queue's dead-letter configuration.

> [!NOTE]
> This timeout is separate from the per-message `MessageProperties.Ttl`. `Ttl` is an AMQP-level setting that controls two things: (1) the maximum time a message may wait in the queue before RabbitMQ expires it, and (2) the publisher-side timeout for `SendReceiveAsync` calls. `MessageProcessingTimeout` is a consumer-level setting that controls how long the handler is allowed to run **after** the message has already been delivered — it is enforced independently of any TTL on the message.

```csharp
.WithMessageProcessingTimeout(TimeSpan.FromSeconds(30))
```

> **Note**: The timeout is enforced through cooperative cancellation. Your handler must observe the `CancellationToken` for the timeout to take effect promptly.

---

## Custom Consumer Arguments

`.WithCustomArgument(string key, object? value)`

Passes arbitrary AMQP consumer arguments to the broker when the consumer is registered. Use this for broker-specific extensions not covered by the built-in options.

```csharp
.WithCustomArgument("x-priority", 10)
```

Multiple arguments can be chained:

```csharp
.WithCustomArgument("x-priority", 10)
.WithCustomArgument("x-cancel-on-ha-failover", true)
```

---

## Complete Example

```csharp
builder.Queues.AddQuorum<OrdersQueue>()
    .WithConsumer(c => c
        .WithSingleAck()
        .WithPrefetchCount(20)
        .WithMessageProcessingTimeout(TimeSpan.FromSeconds(60))
        .WithCustomArgument("x-priority", 5));
```
