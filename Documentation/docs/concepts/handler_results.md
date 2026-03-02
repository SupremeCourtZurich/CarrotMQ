---
uid: concepts-handler-results
title: Handler Results
---

# Handler Results

Every message handler in CarrotMQ returns an `IHandlerResult`. The result tells the framework how to **acknowledge the message** on the AMQP channel and, for command/query handlers, whether to send a response back to the caller.

Handler result types are defined in `CarrotMQ.Core`. The recommended way to return them is through the helper methods provided by `HandlerBase`.

---

## Result Types

### Ok()

```csharp
return Ok();
```

The message was processed successfully. CarrotMQ will **acknowledge** (`basic.ack`) the message, removing it from the queue permanently.

Use `Ok()` in event handlers where no reply is expected.

---

### Ok(TResponse response)

```csharp
return Ok(new MyCommand.Response { OrderId = newOrderId });
```

The message was processed successfully **and** a response is sent back to the caller. CarrotMQ acknowledges the message and publishes the response to the reply endpoint specified in the incoming message's `reply-to` AMQP property.

Use `Ok(response)` in `ICommand` and `IQuery` handlers.

---

### Retry()

```csharp
return Retry();
```

The message could not be processed right now but may succeed if tried again. CarrotMQ will **negatively acknowledge** (`basic.nack`) the message with `requeue: true`, placing it back at the head of the queue.

> [!WARNING]
> Returning `Retry()` immediately without a delay will cause the message to be re-delivered at high speed, potentially creating a tight loop that saturates the consumer. Always introduce a delay — either by sleeping before returning `Retry()` or by using a dead-letter / delayed-message pattern — to allow transient conditions to resolve.

```csharp
// Recommended: delay before re-queuing
await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
return Retry();
```

`RetryResult` is accessible via the `Retry()` helper method on `HandlerBase`.

---

### Reject()

```csharp
return Reject();
```

The message is **permanently rejected**. CarrotMQ will issue `basic.reject` with `requeue: false`. The message is discarded from the queue.

If the queue has a [dead-letter exchange (DLX)](https://www.rabbitmq.com/dlx.html) configured, RabbitMQ will forward the rejected message to that exchange, allowing it to be inspected, archived, or retried via a separate mechanism.

Use `Reject()` when:
- The message is malformed or invalid and will never succeed regardless of retries.
- You want to route failed messages to a dead-letter exchange for observability.

`RejectResult` is accessible via the `Reject()` helper method on `HandlerBase`.

---

### Unhandled Exceptions (automatic framework behaviour)

When an exception propagates out of a handler without being caught:

1. CarrotMQ catches the exception at the message-distributor level.
2. For **command and query handlers**, the framework sets up a `RejectResult` with a `CarrotError` describing the failure. If the message had a reply endpoint, a `500` error response is sent back to the caller so it receives a structured failure rather than timing out.
3. The message is **rejected** (`basic.reject`, requeue=false) and removed from the queue. If a dead-letter exchange is configured on the queue, the message is routed there.

> [!IMPORTANT]
> Returning a structured error result (via `Error()`, `BadRequest()`, or `Cancel()`) **acknowledges** the message (`basic.ack`) and removes it from the queue **without** routing it to the dead-letter exchange. Letting an unhandled exception propagate **rejects** the message (`basic.reject`) and routes it to the DLX if one is configured. Choose the appropriate path depending on whether the message should be dead-lettered on failure.

You do not need to handle this framework behaviour explicitly. Define a dead-letter exchange on the queue if you want failed messages captured for inspection.

---

## Summary

| Result | ACK Behaviour | Sends Reply | When to Use |
|---|---|---|---|
| `Ok()` | `basic.ack` | No | Event handler — success |
| `Ok(response)` | `basic.ack` | Yes | Command/query handler — success with response |
| `Retry()` | `basic.nack` requeue=true | No | Transient failure — try again later |
| `Reject()` | `basic.reject` requeue=false | No | Permanent failure — discard or DLX |
| `BadRequest(...)` | `basic.ack` | Yes (400 error) | Validation failure in command/query handler |
| `Error(...)` | `basic.ack` | Yes (500 or custom error) | Structured error without throwing — command/query only |
| `Cancel()` | `basic.ack` | Yes (504 Gateway Timeout) | Processing cancelled — command/query only |
| Unhandled exception | `basic.reject` requeue=false (framework) | Yes (500 error payload, if reply endpoint configured) | Exception escapes the handler — automatic |

---

## Handler Class Hierarchy

CarrotMQ provides specialised base classes for each message type. You should always inherit from the specific subclass that matches your handler's intent:

```
HandlerBase<TMessage, TResponse>
├── EventHandlerBase<TEvent>
├── RequestHandlerBase<TRequest, TResponse>
│   ├── CommandHandlerBase<TCommand, TResponse>
│   └── QueryHandlerBase<TQuery, TResponse>
└── ResponseHandlerBase<TRequest, TResponse>
```

| Base class | Use for | Extra helpers |
|---|---|---|
| `EventHandlerBase<TEvent>` | Event consumers — no reply expected | — |
| `CommandHandlerBase<TCommand, TResponse>` | Command handlers — mutate state, return a response | `BadRequest()`, `Error()`, `Cancel()` |
| `QueryHandlerBase<TQuery, TResponse>` | Query handlers — read-only, return a response | `BadRequest()`, `Error()`, `Cancel()` |
| `ResponseHandlerBase<TRequest, TResponse>` | Handling replies to commands/queries sent with a `QueueReplyEndPoint` | — |

> [!NOTE]
> `HandlerBase<TMessage, TResponse>` is the common root. You should never inherit from it directly. Always use one of the four concrete subclasses above.

---

## Using HandlerBase

Each specialised handler base class provides convenience methods so you never need to instantiate result types directly:

```csharp
public class CreateOrderHandler
    : CommandHandlerBase<CreateOrderCommand, CreateOrderCommand.Response>
{
    public override async Task<IHandlerResult> HandleAsync(
        CreateOrderCommand command,
        ConsumerContext consumerContext,
        CancellationToken cancellationToken)
    {
        if (!IsValid(command))
            return BadRequest("Validation failed");

        try
        {
            var orderId = await _orderService.CreateAsync(command);
            return Ok(new CreateOrderCommand.Response { OrderId = orderId });
        }
        catch (TransientException)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return Retry();           // HandlerBase.Retry()
        }
    }
}
```

---

## Structured Error Helpers (Command/Query Handlers)

`CommandHandlerBase` and `QueryHandlerBase` expose additional helper methods for returning structured error responses without throwing an exception:

### BadRequest()

```csharp
return BadRequest("Validation failed", new Dictionary<string, string[]>
{
    { "Name", ["Name is required."] }
});
```

Returns a `400 Bad Request` response to the caller. The optional second parameter is a dictionary of field-level validation errors (`IDictionary<string, string[]>`).

### Error()

```csharp
return Error("Something went wrong");
```

Returns a `500 Internal Server Error` response. Unlike an unhandled exception (which rejects the message and may route it to a DLX), `Error()` **acknowledges** the message so it is removed from the queue without dead-lettering. Use this when you want to send a structured error response while still consuming the message definitively.

### Error(statusCode, response, ...)

```csharp
return Error(422, response: null, "Unprocessable entity");
```

Returns a custom status code. Use this when you need a specific HTTP-style code (e.g. `422 Unprocessable Entity`, `403 Forbidden`) that doesn't map to one of the named helpers.

### Cancel()

```csharp
return Cancel();
```

Returns a `504 Gateway Timeout` response to the caller, signalling that processing was cancelled before a result could be produced (for example, because the `CancellationToken` was triggered). Only available in `CommandHandlerBase` and `QueryHandlerBase`.

> [!NOTE]
> Prefer `BadRequest()`, `Error()`, and `Cancel()` over constructing `ErrorResult` directly. The helpers produce the correct status codes and keep your handler code idiomatic.

---

## ConsumerContext

Every `HandleAsync` method receives a `ConsumerContext` as its second parameter. It carries metadata about the incoming message:

| Property | Type | Description |
|---|---|---|
| `MessageId` | `Guid` | Unique identifier for this message, assigned at publish time. |
| `CorrelationId` | `Guid?` | Correlates a response back to the original request. Present on response messages. |
| `CreatedAt` | `DateTimeOffset` | Timestamp when the message was created by the publisher. |
| `MessageProperties` | `MessageProperties` | AMQP-level properties on the message, including `Ttl`, `Priority`, `Persistent`, and `PublisherConfirm`. |
| `CustomHeader` | `IDictionary<string, string>?` | Arbitrary key/value pairs forwarded from the publisher via `Context.CustomHeader` or a message enricher. |
| `InitialUserName` | `string?` | Identity of the user who originated the message chain (set via `Context.InitialUserName`). |
| `InitialServiceName` | `string?` | Name of the service that first published the message chain (set via `Context.InitialServiceName`). |

```csharp
public override async Task<IHandlerResult> HandleAsync(
    MyCommand command,
    ConsumerContext context,
    CancellationToken cancellationToken)
{
    var tenantId = context.CustomHeader?["X-Tenant-Id"];
    var publishedBy = context.InitialServiceName;

    // ...
    return Ok(response);
}
```

---

## Handler DI Lifetime

Handlers are registered as **transient** in the DI container — a new instance is created for each incoming message.

More precisely, CarrotMQ creates a fresh **DI scope** for every message it processes. Within that scope, the handler is resolved as a transient. This means:

- **Scoped services** (e.g. `DbContext`, `IRepository`) can be constructor-injected safely — each message gets its own scope and therefore its own instance.
- **Singleton services** are shared across all messages as normal.
- The scope is disposed after the handler returns, releasing any scoped resources.

```csharp
// Safe: DbContext is scoped — each message gets its own instance
public class CreateOrderHandler : CommandHandlerBase<CreateOrderCommand, CreateOrderCommand.Response>
{
    private readonly AppDbContext _db;

    public CreateOrderHandler(AppDbContext db)
        => _db = db;

    public override async Task<IHandlerResult> HandleAsync(
        CreateOrderCommand command,
        ConsumerContext context,
        CancellationToken cancellationToken)
    {
        _db.Orders.Add(new Order { ... });
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new CreateOrderCommand.Response { ... });
    }
}
```
