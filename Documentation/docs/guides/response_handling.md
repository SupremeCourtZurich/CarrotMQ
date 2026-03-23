# Response Handling

When a command or query is sent with a `QueueReplyEndPoint`, the response arrives asynchronously on a reply queue. `ResponseHandlerBase<TRequest, TResponse>` is the structured way to consume those replies.

---

## When to Use `ResponseHandlerBase` vs `SendReceiveAsync`

| | `SendReceiveAsync` | `ResponseHandlerBase` |
|---|---|---|
| **Caller blocks?** | Yes | No |
| **Reply queue needed?** | No | Yes |
| **Coupling** | Tight (inline) | Loose (separate handler class) |
| **Best for** | Short, synchronous operations | Async workflows, long-running ops, separate services |

Use `ResponseHandlerBase` when:
- The response may take a long time to arrive.
- The component that sends the command and the component that handles the reply are logically or physically separate.
- You want to apply the same handler logic regardless of which caller triggered the command.

---

## Implementing `ResponseHandlerBase`

Subclass `ResponseHandlerBase<TRequest, TResponse>` and override `HandleAsync`:

```csharp
public class MyCommandResponseHandler
    : ResponseHandlerBase<MyCommand, MyCommand.Response>
{
    public override async Task<IHandlerResult> HandleAsync(
        CarrotResponse<MyCommand, MyCommand.Response> message,
        ConsumerContext consumerContext,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Response: {message.Content?.ResponseMessage}");
        return Ok();
    }
}
```

- `message.StatusCode` — HTTP-like status code set by the handler (e.g. `200`, `400`, `500`).
- `message.Content` — the strongly-typed response payload (`MyCommand.Response`).
- `message.Request` — the original request message that was sent.
- `message.Error` — a `CarrotError` containing a human-readable message and optional field-level validation errors.

Return one of the built-in `IHandlerResult` values:

| Result | Meaning |
|---|---|
| `Ok()` | Message processed successfully; acknowledge to broker |
| `Retry()` | Processing failed temporarily; negatively acknowledge with requeue |
| `Reject()` | Processing failed permanently; do not requeue |

---

## Registering the Handler

Add the response handler during CarrotMQ setup:

```csharp
builder.Handlers.AddResponse<MyCommandResponseHandler, MyCommand, MyCommand.Response>();
```

This registers `MyCommandResponseHandler` in the DI container and wires it to the response pipeline for `MyCommand`.

> [!NOTE]
> The handler is instantiated per-message using the DI container, so constructor injection of services such as `ILogger<T>` or `IRepository` is fully supported.

---

## Setting Up the Reply Queue

For the response to be delivered, a reply queue must exist and be consumed. This is typically done in the same bootstrap code that registers handlers:

```csharp
builder.Queues.AddClassic("my-reply-queue").WithConsumer();
```

The queue name must match the `QueueReplyEndPoint` used when sending the command:

```csharp
await carrotClient.SendAsync(
    new MyCommand { Message = "Do it" },
    new QueueReplyEndPoint("my-reply-queue"));
```

> [!IMPORTANT]
> Without a queue declaration and an active consumer, replies will be silently dropped by the broker and `HandleAsync` will never be called.

---

## The `CarrotResponse<TRequest, TResponse>` Object

| Property | Type | Description |
|---|---|---|
| `StatusCode` | `int` | HTTP-like status code from the handler (200 = success) |
| `Content` | `TResponse?` | Typed response payload; `null` on error |
| `Request` | `TRequest?` | The original request message |
| `Error` | `CarrotError?` | Error details including message and field-level errors |

### Inspecting Errors

When `StatusCode` indicates a failure, check `Error` for details:

```csharp
if (response.StatusCode != 200)
{
    Console.WriteLine($"Error: {response.Error?.Message}");

    foreach (var field in response.Error?.Errors ?? [])
        Console.WriteLine($"  {field.Key}: {string.Join(", ", field.Value)}");
}
```

---

## Full Example

```csharp
// 1. Define the command (in the shared Dto project)
public class MyCommand : ICommand<MyCommand, MyCommand.Response, MyQueue>
{
    public string Message { get; set; }

    public class Response
    {
        public string ResponseMessage { get; set; }
    }
}

// 2. Implement the response handler (in the reply-consuming service)
public class MyCommandResponseHandler
    : ResponseHandlerBase<MyCommand, MyCommand.Response>
{
    private readonly ILogger<MyCommandResponseHandler> _logger;

    public MyCommandResponseHandler(ILogger<MyCommandResponseHandler> logger)
        => _logger = logger;

    public override async Task<IHandlerResult> HandleAsync(
        CarrotResponse<MyCommand, MyCommand.Response> message,
        ConsumerContext consumerContext,
        CancellationToken cancellationToken)
    {
        if (message.StatusCode == 200)
            _logger.LogInformation("Success: {Msg}", message.Content?.ResponseMessage);
        else
            _logger.LogWarning("Failed: {Err}", message.Error?.Message);

        return Ok();
    }
}

// 3. Register handler and reply queue during bootstrap
builder.Queues.AddClassic("my-reply-queue").WithConsumer();

builder.Handlers.AddResponse<MyCommandResponseHandler, MyCommand, MyCommand.Response>();

// 4. Send the command with the reply endpoint
await carrotClient.SendAsync(
    new MyCommand { Message = "Do it" },
    new QueueReplyEndPoint("my-reply-queue"));
```
