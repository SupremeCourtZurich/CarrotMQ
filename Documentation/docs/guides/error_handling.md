# Error Handling

CarrotMQ provides structured error handling at two levels: exceptions thrown directly by `ICarrotClient` during publishing, and error responses returned by remote handlers.

---

### Handler exceptions

When a handler throws an unhandled exception, the framework catches it and, if the caller expected a reply, sends an error response back automatically. The call site receives a `CarrotResponse` with:

- `StatusCode` set to `500`
- `Error` populated with a `CarrotError` describing what went wrong

The message is **rejected** (`basic.reject`, requeue=false) so it is not re-queued. If a Dead Letter Exchange is configured on the queue, the message is routed there; otherwise it is dropped.

---

### AMQP acknowledgement semantics

The `IHandlerResult` a handler returns controls how CarrotMQ acknowledges the message to the broker:

| Result / Scenario | AMQP action | Requeued? | Notes |
|---|---|---|---|
| `Ok()` | `basic.ack` | No | Message consumed successfully. Removed from the queue. |
| `Retry()` | `basic.nack` (requeue=true) | Yes | Redelivered to the same queue. Use for transient failures (e.g. downstream temporarily unavailable). |
| `Reject()` | `basic.reject` (requeue=false) | No | Permanently rejected. If a Dead Letter Exchange (DLX) is configured, the message is routed there; otherwise it is dropped. Use for unrecoverable errors (e.g. invalid message format). |
| `BadRequest(...)` / `Error(...)` / `Cancel()` | `basic.ack` | No | Message acknowledged and removed from the queue. An error response (400/500/504) is sent to the caller. The message is **not** routed to a DLX. |
| Unhandled exception (command/query) | `basic.reject` (requeue=false) | No | A `500` error response is sent to the caller's reply queue, then the message is rejected. **If a DLX is configured on the queue, the message will also be routed to the dead-letter queue** — even though the caller already received the error. |
| Unhandled exception (event) | `basic.reject` (requeue=false) | No | The message is rejected. No response is sent. Routes to DLX if configured. |

> **Choosing between `Retry()` and `Reject()`:**
> - Use `Retry()` when the failure is likely temporary and retrying later makes sense.
> - Use `Reject()` when the message itself is the problem (e.g. it cannot be deserialized or fails invariant validation). Repeated requeuing of an unprocessable message will loop forever without a DLX or retry limit.

> [!WARNING]
> **AutoAck mode:** If a consumer is configured with AutoAck enabled, returning `Reject()` or `Retry()` has **no effect** — the broker automatically acknowledges every message as soon as it is delivered, before the handler runs. Dead-lettering and requeue behaviour are silently bypassed. Only use AutoAck for non-critical consumers where message loss is acceptable.

---

### CarrotResponse structure

`CarrotResponse<TRequest, TResponse>` carries both the success payload and any error information:

| Property | Description |
|---|---|
| `StatusCode` | HTTP-style status code. `200` indicates success; non-200 values indicate an error or failure (e.g. `400` for bad request, `500` for internal error). |
| `Content` | The typed response payload (`TResponse`). `null` when an error occurred. |
| `Request` | The original request object (`TRequest`). |
| `Error` | A `CarrotError` instance when an error occurred; `null` on success. |

**CarrotError properties:**

| Property | Description |
|---|---|
| `Message` | A human-readable description of the error. |
| `Errors` | A dictionary of field-level validation errors. Keys are field paths; values are arrays of error messages for that field. |

---

### Checking the response

Always inspect `StatusCode` before using `Content`. Use the constants on `CarrotStatusCode` instead of magic integers to make intent clear:

```csharp
var response = await carrotClient.SendReceiveAsync(new MyCommand { /* ... */ });

if (response.StatusCode == CarrotStatusCode.Ok)
{
    // Safe to use response.Content here
}
else
{
    Console.WriteLine($"Error ({response.StatusCode}): {response.Error?.Message}");

    foreach (var field in response.Error?.Errors ?? [])
        Console.WriteLine($"  {field.Key}: {string.Join(", ", field.Value)}");
}
```

**`CarrotStatusCode` constants:**

| Constant | Value | Meaning |
|---|---|---|
| `CarrotStatusCode.Ok` | `200` | Success |
| `CarrotStatusCode.BadRequest` | `400` | Validation failure |
| `CarrotStatusCode.Unauthorized` | `401` | Caller is not authenticated |
| `CarrotStatusCode.Forbidden` | `403` | Caller lacks permission |
| `CarrotStatusCode.GatewayTimeout` | `504` | Handler was cancelled before completing |
| `CarrotStatusCode.InternalServerError` | `500` | Unhandled exception in the handler |

---

### Producing error results from handlers

Handlers can signal an error in two ways:

1. **Throw an exception** — the framework catches it, rejects the message (`basic.reject`), and, for command/query handlers that have a reply endpoint, sends a `500` error response back to the caller automatically. Event handler exceptions are also rejected but no response is sent.
2. **Return a structured error result** — use the helper methods on `CommandHandlerBase` or `QueryHandlerBase` to return an error with a specific status code and optional field-level validation errors, without throwing:

   ```csharp
   // 400 Bad Request with field-level validation errors
   return BadRequest("Validation failed", new Dictionary<string, string[]>
   {
       { "Name", ["Name is required."] }
   });

   // 500 Internal Server Error
   return Error("Something went wrong");

   // Custom status code
   return Error(422, response: null, "Unprocessable entity");

   // 504 Gateway Timeout — processing was cancelled
   return Cancel();
   ```

   Prefer these helpers over constructing `ErrorResult` directly. They produce the correct status codes and are available on `CommandHandlerBase` and `QueryHandlerBase`.

Middleware can also influence error handling by setting `context.IsErrorResult = true` on the `MiddlewareContext` and adjusting `context.DeliveryStatus` as needed. See the [Middleware](middleware.md) guide for details.

---

### Exceptions thrown by ICarrotClient

`ICarrotClient` itself can throw the following exceptions during a send or send-receive operation:

| Exception | When it is thrown |
|---|---|
| `OperationCanceledException` | The configured TTL expired before a response was received, or the provided `CancellationToken` was cancelled. |
| `RetryLimitExceededException` | Publisher confirms could not be obtained within the configured retry limit. The message may or may not have been delivered. |
| `ArgumentException` | The message has invalid properties — for example, a `ICustomRoutingEvent` was published with an exchange name that contains illegal characters. |

Wrap `SendReceiveAsync` (and fire-and-forget send calls in critical paths) in a `try/catch` to handle these cases gracefully:

```csharp
try
{
    var response = await carrotClient.SendReceiveAsync(new MyQuery { /* ... */ });
    // handle response
}
catch (OperationCanceledException)
{
    // TTL expired or cancellation was requested
}
catch (RetryLimitExceededException)
{
    // Could not confirm delivery — consider compensating action
}
catch (ArgumentException ex)
{
    // Invalid message configuration
    logger.LogError(ex, "Message configuration error");
}
```
