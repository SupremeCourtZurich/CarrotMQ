# Connection Lifecycle

This page explains what happens at three critical points in a CarrotMQ service's lifetime: initial startup, mid-run connection loss, and controlled shutdown. Understanding this behaviour is essential for operating a service reliably in production.

---

## Initial Connection

When the hosted service starts, CarrotMQ calls `BrokerConnection.ConnectAsync()`, which enters a retry loop governed by `InitialConnectionTimeout`.

**How the retry loop works:**

1. CarrotMQ attempts `ConnectionFactory.CreateConnectionAsync()`.
2. On `BrokerUnreachableException` it logs the error and checks whether `InitialConnectionTimeout` has elapsed.
3. If not elapsed, it waits `NetworkRecoveryInterval` then retries.
4. If the timeout *has* elapsed, it re-throws `BrokerUnreachableException` and the hosted service fails to start.

### Default behaviour — `InfiniteTimeSpan`

The default value of `InitialConnectionTimeout` is `Timeout.InfiniteTimeSpan`. This means the service **blocks indefinitely** until RabbitMQ becomes available. This is the right default for container orchestration (Docker Compose, Kubernetes) where your service and RabbitMQ may start in parallel:

```json
// appsettings.json — no override needed; InfiniteTimeSpan is the default
{
  "BrokerConnection": {
    "BrokerEndPoints": ["amqp://rabbitmq:5672"]
  }
}
```

### Finite timeout

Set a finite timeout when you want the .NET host to fault immediately if the broker is unavailable at startup rather than waiting forever:

```json
{
  "BrokerConnection": {
    "BrokerEndPoints": ["amqp://rabbitmq:5672"],
    "InitialConnectionTimeout": "00:00:30"
  }
}
```

Or in code:

```csharp
builder.ConfigureBrokerConnection(options =>
{
    options.InitialConnectionTimeout = TimeSpan.FromSeconds(30);
});
```

When the timeout expires, `BrokerUnreachableException` is thrown. The .NET host treats this as a fatal startup error and terminates.

> **Tip:** Prefer `InfiniteTimeSpan` in orchestrated environments and a finite timeout in development so that misconfigured endpoints surface quickly.

---

## Runtime Connection Loss & Automatic Recovery

### Connection-level recovery

CarrotMQ uses the `RabbitMQ.Client` built-in automatic recovery. When the TCP connection is lost, the client library:

1. Detects the outage.
2. Reconnects to a broker node (honouring `BrokerEndPoints` and `RandomizeEndPointResolving`).
3. Re-declares topology (exchanges, queues, bindings) from its in-memory snapshot.
4. Re-registers all active consumers.

This happens transparently — no CarrotMQ code is involved. A `Connection shut down` warning is logged when the connection drops, and `AutoRecovering connection succeeded` is logged when recovery completes.

### Channel-level recovery (AMQP errors)

AMQP errors with a reply code ≥ 400 close the channel without closing the connection. CarrotMQ's `CarrotChannel` layer detects these and runs its own channel recovery loop, reopening the channel and resuming operation.

### Consumer re-registration

After a connection recovery event, `CarrotConsumer.ConsumerChannelUnregisteredAsync()` fires. It:

1. Stops the current consumer (sends `basic.cancel` if the channel is still open, drains in-flight tasks).
2. Waits one `NetworkRecoveryInterval`.
3. Calls `StartAsync()` to create a new consumer channel and re-register with the broker.

### In-flight messages during connection loss

What happens to a message being processed when the connection drops depends on the acknowledgement mode:

| Ack mode | Connection drops mid-processing | Outcome |
|---|---|---|
| `WithSingleAck()` | Ack not sent before connection closes | RabbitMQ requeues the message; it is redelivered to the next available consumer |
| `WithAckCount(n)` | Batch ack not yet sent | All unacknowledged messages in the batch are requeued and redelivered |
| `WithAutoAck()` | Message was acked on delivery by RabbitMQ | Message is **lost** — the broker considers it delivered the moment it was dispatched |

> **Warning:** `WithAutoAck()` sacrifices delivery guarantees. Prefer `WithSingleAck()` or `WithAckCount(n)` for any message that must not be lost.

---

## Graceful Shutdown

### Shutdown sequence

When the .NET host stops (e.g. `Ctrl+C`, `SIGTERM`, rolling deployment), the following sequence occurs:

1. The host cancels `stoppingToken`.
2. `CarrotService.ExecuteAsync()` catches the resulting `TaskCanceledException`.
3. `CarrotConsumerManager.StopConsumingAsync()` is called, which calls `DisposeAsync()` on every `CarrotConsumer`.
4. Each consumer calls `ConsumerChannel.StopConsumingAsync()`, which:
   a. Sends `basic.cancel` to the broker — the broker stops delivering new messages to this consumer.
   b. Calls `RunningTaskRegistry.CompleteAddingAsync()` — **blocks until every in-flight handler task has completed**.
5. Once all consumers have drained, the connection is closed cleanly.

### Drain guarantee

CarrotMQ waits for every in-flight handler to finish before closing channels. No acknowledged message is left mid-processing by the library itself.

### Hard deadline — host `ShutdownTimeout`

CarrotMQ has no configurable drain timeout of its own. The only hard deadline is the .NET host's `ShutdownTimeout`, which defaults to **30 seconds**. If the drain takes longer than this, the host forcefully terminates the process.

Extend the timeout if your handlers need more time to complete:

```csharp
services.Configure<HostOptions>(o =>
{
    o.ShutdownTimeout = TimeSpan.FromMinutes(2);
});
```

> **Warning:** With `WithAutoAck()`, messages that arrive after `basic.cancel` is sent but before the consumer fully stops may be dropped silently. Use manual ack modes if you need a clean shutdown guarantee.
