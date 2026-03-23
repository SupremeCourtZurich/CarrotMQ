# Subscriptions

Subscriptions are a lightweight alternative to writing a full handler class. Instead of creating a dedicated `EventHandlerBase` or `RequestHandlerBase` subclass, you inject a subscription object into an existing service and attach a delegate to its event.

This keeps your message handling co-located with the service that acts on the result, which is often the most natural fit for background workers or services that both initiate and react to messages.

> [!NOTE]
> Under the hood, CarrotMQ registers a thin handler that delegates to the subscription object. All the usual routing, acknowledgement, and middleware pipeline still apply.

---

### EventSubscription\<TEvent\>

`EventSubscription<TEvent>` lets you react to incoming events from within any class that can receive constructor injection.

#### Registering

Call `AddEventSubscription<TEvent>()` on the handler collection when configuring the CarrotMQ builder, and chain `.BindTo(exchange, queue)` to declare the routing. Use the builder objects returned by `builder.Exchanges` and `builder.Queues`:

```csharp
DirectExchangeBuilder exchange = builder.Exchanges.AddDirect<MyExchange>();
QuorumQueueBuilder queue = builder.Queues.AddQuorum<MyQueue>().WithConsumer();

builder.Handlers
    .AddEventSubscription<MyEvent>()
    .BindTo(exchange, queue);
```

`AddEventSubscription` automatically registers `EventSubscription<MyEvent>` as a singleton in the DI container, so no manual `services.AddSingleton<...>()` call is needed.

#### Subscribing to events

Inject `EventSubscription<TEvent>` wherever the handling logic lives and attach a handler to `EventReceived`:

```csharp
public class MyService : IHostedService
{
    public MyService(EventSubscription<MyEvent> subscription)
    {
        subscription.EventReceived += async (sender, args) =>
        {
            Console.WriteLine(args.Event?.Message);
            await Task.CompletedTask;
        };
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

The `args` parameter is of type `EventSubscriptionEventArgs<TEvent>` and exposes:

| Property | Description |
|---|---|
| `Event` | The deserialized event message. |
| `ConsumerContext` | Metadata such as `MessageId`, `CorrelationId`, `CreatedAt`, and `CustomHeader`. |

#### Custom routing events

If your event implements `ICustomRoutingEvent`, use `AddCustomRoutingEventSubscription<T>()` instead:

```csharp
builder.Handlers.AddCustomRoutingEventSubscription<MyCustomRoutingEvent>();
```

The injection and subscription pattern is identical to `EventSubscription<TEvent>`.

---

### ResponseSubscription\<TRequest, TResponse\>

`ResponseSubscription<TRequest, TResponse>` follows the same pattern for handling responses asynchronously, without writing a dedicated response handler class.

#### Registering

```csharp
builder.Handlers.AddResponseSubscription<MyQuery, MyQuery.Response>();
```

#### Subscribing to responses

```csharp
public class MyService
{
    public MyService(ResponseSubscription<MyQuery, MyQuery.Response> subscription)
    {
        subscription.ResponseReceived += async (sender, args) =>
        {
            Console.WriteLine(args.Response.Content?.ResponseMessage);
            await Task.CompletedTask;
        };
    }
}
```

The `args` parameter is of type `ResponseSubscriptionEventArgs<TRequest, TResponse>` and exposes:

| Property | Description |
|---|---|
| `Response` | The full `CarrotResponse<TRequest, TResponse>`, including `StatusCode`, `Content`, `Request`, and `Error`. |
| `ConsumerContext` | Message metadata. |

---

### Full Example: `SendAsync` + `ResponseSubscription`

This end-to-end example shows a service that both sends a command and reacts to the response using `ResponseSubscription`, all within a `BackgroundService`.

```csharp
// 1. Define the command (shared DTO project)
public class ProcessOrderCommand : ICommand<ProcessOrderCommand, ProcessOrderCommand.Response, OrderQueue>
{
    public int OrderId { get; set; }

    public class Response
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}

// 2. Bootstrap — register the subscription, reply queue, and connect handler
services.AddCarrotMqRabbitMq(builder =>
{
    builder.ConfigureBrokerConnection(sectionName: "BrokerConnection");

    // Reply queue where responses arrive
    builder.Queues.AddClassic("my-service-replies").WithConsumer();

    // Register the response subscription
    builder.Handlers.AddResponseSubscription<ProcessOrderCommand, ProcessOrderCommand.Response>();

    builder.StartAsHostedService();
});

// 3. Inject and use in a BackgroundService
public class OrderWorker : BackgroundService
{
    private readonly ICarrotClient _client;
    private readonly ResponseSubscription<ProcessOrderCommand, ProcessOrderCommand.Response> _subscription;

    public OrderWorker(
        ICarrotClient client,
        ResponseSubscription<ProcessOrderCommand, ProcessOrderCommand.Response> subscription)
    {
        _client = client;
        _subscription = subscription;

        // Attach handler once — the subscription lives for the application lifetime
        _subscription.ResponseReceived += async (_, args) =>
        {
            if (args.Response.StatusCode == 200)
                Console.WriteLine($"Order processed: {args.Response.Content?.Message}");
            else
                Console.WriteLine($"Failed: {args.Response.Error?.Message}");

            await Task.CompletedTask;
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _client.SendAsync(
                new ProcessOrderCommand { OrderId = 42 },
                new QueueReplyEndPoint("my-service-replies"),
                cancellationToken: stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
```

> [!IMPORTANT]
> The reply queue name in `QueueReplyEndPoint("my-service-replies")` must exactly match the queue declared with `builder.Queues.AddClassic("my-service-replies")`. Without an active consumer on this queue, responses will accumulate but `ResponseReceived` will never fire.

---

### When to use subscriptions vs. handler classes

| | Subscriptions | Handler classes |
|---|---|---|
| **Best for** | Handling tightly coupled to an existing service (e.g., a `BackgroundService` that also sends the request) | Dedicated, reusable message-handling logic |
| **Separation of concerns** | Co-located with the consuming service | Separate class per message type |
| **Testability** | Tested together with the host service | Easily unit-tested in isolation |
| **Complexity** | Ideal for simple, single-concern reactions | Recommended when logic grows complex |

Use subscriptions when the class receiving the message is the same class that initiated the request or needs to act on the result directly. Prefer handler classes when the handling logic is independent, reusable, or non-trivial.

---

### Lifecycle and DI Details

Both `EventSubscription<TEvent>` and `ResponseSubscription<TRequest, TResponse>` are registered as **singletons**. The underlying handler that delegates to them is registered as **transient** (one instance per message). This means:

- The subscription object itself lives for the application lifetime and is shared across all messages.
- Any delegates attached to `EventReceived` or `ResponseReceived` persist for the application lifetime. Attach them once at construction time (e.g. in a constructor or `StartAsync`) and do not re-attach on every message.
- Because the subscription is a singleton, avoid capturing scoped services directly in the delegate. If you need a scoped dependency, resolve it explicitly inside the delegate body.

---

### Exception Handling in Delegates

If a delegate attached to `EventReceived` or `ResponseReceived` throws an exception, CarrotMQ collects all exceptions from all attached handlers and re-throws them as an `AggregateException`. This propagates up through the message processing pipeline.

```csharp
subscription.EventReceived += async (sender, args) =>
{
    try
    {
        await ProcessAsync(args.Event);
    }
    catch (Exception ex)
    {
        // Handle or log — unhandled exceptions here will cause the message to be rejected
        _logger.LogError(ex, "Failed to process event");
        throw; // re-throw to signal failure to CarrotMQ
    }
};
```

To acknowledge the message successfully, all attached delegates must complete without throwing. If any delegate throws, the message processing is considered failed.

---

### ConsumerContext Properties

The `ConsumerContext` available in subscription event args exposes the following metadata:

| Property | Type | Description |
|---|---|---|
| `MessageId` | `Guid` | Unique identifier for this message. |
| `CorrelationId` | `Guid?` | Correlation ID linking a response to the original request. Present on response messages. |
| `CreatedAt` | `DateTimeOffset` | Timestamp when the message was originally created by the publisher. |
| `MessageProperties` | `MessageProperties` | AMQP-level properties on the message (`Ttl`, `Priority`, `Persistent`, `PublisherConfirm`). |
| `CustomHeader` | `IDictionary<string, string>?` | Arbitrary key/value metadata forwarded by the publisher via `Context.CustomHeader`. |
| `InitialUserName` | `string?` | Identity of the user who originated the message chain. |
| `InitialServiceName` | `string?` | Name of the service that first published the message chain. |
