# Message Enricher

The message enricher is a delegate that CarrotMQ invokes automatically before every outgoing message is sent through `ICarrotClient`. It gives you a single, centralised place to add headers or metadata to all messages without modifying each call site.

---

### Registering a message enricher

Call `AddMessageEnricher` on your `IServiceCollection` during application startup:

```csharp
services.AddMessageEnricher((message, context, cancellationToken) =>
{
    // Add a custom header to every outgoing message
    context.CustomHeader["X-Tenant-Id"] = "my-tenant";
});
```

The delegate receives three parameters:

| Parameter | Type | Description |
|---|---|---|
| `message` | `object` | The outgoing message object. |
| `context` | `Context` | The outgoing message context. Use `context.CustomHeader` to attach arbitrary key/value metadata. |
| `cancellationToken` | `CancellationToken` | Cancellation token passed from the calling operation. |

> [!NOTE]
> Only one message enricher can be registered at a time. If you need to apply multiple enrichment steps, combine them inside a single delegate.

> [!IMPORTANT]
> The `AddMessageEnricher` overload accepts a **synchronous** `Action` delegate — async operations (e.g. `await`) are not supported inside it. If you need to perform async work during enrichment (e.g. reading from a cache or external store), implement the `IMessageEnricher` interface directly and register it with `services.AddSingleton<IMessageEnricher, MyAsyncEnricher>()`.

---

### Implementing `IMessageEnricher` for async enrichment

When you need `await` inside your enricher, implement `IMessageEnricher` directly:

```csharp
public class TenantCacheEnricher : IMessageEnricher
{
    private readonly ITenantCache _cache;

    public TenantCacheEnricher(ITenantCache cache)
        => _cache = cache;

    public async Task EnrichMessageAsync(object message, Context context, CancellationToken cancellationToken)
    {
        var tenantId = await _cache.GetCurrentTenantIdAsync(cancellationToken);
        context.CustomHeader["X-Tenant-Id"] = tenantId;
    }
}
```

Register it as a singleton in DI — **do not** also call `AddMessageEnricher`:

```csharp
services.AddSingleton<IMessageEnricher, TenantCacheEnricher>();
```

> [!NOTE]
> Only one `IMessageEnricher` can be active at a time. `AddMessageEnricher` registers a `DelegateMessageEnricher` singleton behind the scenes, and a direct `services.AddSingleton<IMessageEnricher, ...>()` replaces it. If you call both, the last registration wins.

---

### Common use cases

- **Tenant propagation** — attach a tenant identifier to every message so downstream services can apply tenant-specific logic without requiring it in every request DTO.
- **User context** — forward the current user's ID or roles from an incoming HTTP request to outbound messages.
- **Request / correlation IDs** — propagate a distributed trace or correlation ID across service boundaries to make log correlation straightforward.

```csharp
services.AddMessageEnricher((message, context, cancellationToken) =>
{
    context.CustomHeader["X-Correlation-Id"] = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString();
    context.CustomHeader["X-Tenant-Id"] = tenantContext.CurrentTenantId;
});
```

Because the enricher runs before every `ICarrotClient` send operation, it applies equally to events, commands, and queries — no per-call wiring required.
