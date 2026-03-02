---
uid: concepts-endpoints
title: Endpoints
---

# Endpoints

Endpoints in CarrotMQ are **strongly-typed wrappers** around AMQP exchange and queue names. Instead of passing raw strings throughout your codebase, you define a named class once and reference it by type — giving you compile-time safety and a single place to change a name if it ever needs to move.

All endpoint base types are defined in `CarrotMQ.Core` and should be placed in your **shared DTO library**.

---

## Exchange Endpoints

### ExchangeEndPoint

`ExchangeEndPoint` is the base class for any AMQP exchange reference. Derive from it to create a named, strongly-typed exchange definition.

```csharp
public class MyExchange : ExchangeEndPoint
{
    public MyExchange() : base("my-exchange") { }
}
```

Exchange endpoints are used as the second type parameter on `IEvent<TEvent, TExchangeEndPoint>`:

```csharp
public class OrderPlacedEvent : IEvent<OrderPlacedEvent, MyExchange> { ... }
```

---

## Queue Endpoints

### QueueEndPoint

`QueueEndPoint` is the base class for any AMQP queue reference. Derive from it to define a named, strongly-typed queue.

```csharp
public class MyQueue : QueueEndPoint
{
    public MyQueue() : base("my-queue") { }
}
```

Queue endpoints are the most common choice for the third type parameter on `ICommand` and `IQuery` (see [Endpoints for Commands and Queries](#endpoints-for-commands-and-queries) below):

```csharp
public class MyCommand : ICommand<MyCommand, MyCommand.Response, MyQueue> { ... }
public class MyQuery   : IQuery<MyQuery, MyQuery.Response, MyQueue>       { ... }
```

Multiple message types can share the same queue endpoint — the handler is resolved by the message's CLR type, not the queue name.

---

## Endpoints for Commands and Queries

The third type parameter of `ICommand<TCommand, TResponse, TEndPointDefinition>` and `IQuery<TQuery, TResponse, TEndPointDefinition>` accepts any class that derives from `EndPointBase` — which includes both `QueueEndPoint` and `ExchangeEndPoint`.

### Recommended: Target a queue directly

For most request/reply scenarios, routing directly to a named queue is the right choice. It guarantees that **exactly one consumer instance** receives each message, which is the expected behaviour for RPC-style interactions.

```csharp
public class MyCommand : ICommand<MyCommand, MyCommand.Response, OrderQueue> { ... }
```

### Advanced: Target an exchange

Use an `ExchangeEndPoint` when you need the broker to perform routing before the message reaches a consumer queue — for example with a `LocalRandomExchange`, which delivers the message to one randomly chosen consumer queue on the same broker node:

```csharp
public class MyCommand : ICommand<MyCommand, MyCommand.Response, OrderLocalRandomExchange> { ... }
```

This is useful when you have multiple consumer instances, each with its own queue, and want the broker to balance the load at the exchange level rather than through a shared queue.

> [!NOTE]
> When targeting an exchange endpoint, CarrotMQ uses the message's CLR type name as the routing key (via `IRoutingKeyResolver`), just as it does for events. You are responsible for ensuring the exchange and its queue bindings exist on the broker.

---

## Reply Endpoints

Reply endpoints control **where the response is delivered** when a consumer processes an `ICommand` or `IQuery`. They are passed as the `replyEndPoint` parameter when calling `SendAsync` on the ICarrotClient.

CarrotMQ provides four built-in reply endpoints:

### Direct Reply (used by SendReceiveAsync)

Uses RabbitMQ's built-in pseudo-queue `amq.rabbitmq.reply-to` for synchronous RPC. The consumer delivers the reply directly back to the calling channel without declaring a temporary queue. This is the most efficient option when the caller blocks waiting for the response.

```csharp
await carrotClient.SendReceiveAsync(new MyCommand { ... });
// SendReceiveAsync uses direct reply internally
```

> [!NOTE]
> Direct reply is used automatically by `SendReceiveAsync`. You do not need to configure it explicitly.

### QueueReplyEndPoint

Sends the reply to a specific, named queue. An optional `includeRequestPayloadInResponse` flag (default `false`) controls whether the original request payload is included in the response.

```csharp
var replyEndPoint = new QueueReplyEndPoint("my-reply-queue");
await carrotClient.SendAsync(new MyCommand { ... }, replyEndPoint);
```

Use this when:
- The caller processes replies asynchronously and needs the reply in a durable or shared queue.
- You want to fan out request correlation across multiple application instances.

### ExchangeReplyEndPoint

Sends the reply to an exchange rather than directly to a queue. Useful when replies need to be routed through a topic or headers exchange before reaching the consumer.

```csharp
var replyEndPoint = new ExchangeReplyEndPoint("my-reply-exchange", routingKey: "replies.orders");
await carrotClient.SendAsync(new MyCommand { ... }, replyEndPoint);
```

### No Reply (fire-and-forget)

Explicitly instructs the consumer **not to send a reply**. The handler's `Ok(response)` return value is discarded. Pass `null` as the `replyEndPoint` argument, or omit it entirely since it is optional for commands.

```csharp
await carrotClient.SendAsync(new MyCommand { ... }); // replyEndPoint defaults to null
```

Use this when you send a command for its side effects only and do not need to observe the response.

---

## SendReceiveAsync vs SendAsync with a Reply Endpoint

CarrotMQ offers two publishing patterns for request/reply messaging:

| Method | Behaviour |
|---|---|
| `SendReceiveAsync(command)` | Sends the message and **blocks** the calling task until the reply arrives. Uses RabbitMQ direct reply-to internally. Best for synchronous request/response flows. |
| `SendAsync(command, replyEndPoint)` | Sends the message and **returns immediately**. The reply is delivered to the specified endpoint and must be consumed separately. Best for async fire-and-monitor flows. |

### Example: Synchronous RPC

```csharp
// Blocks until the handler returns Ok(response)
CarrotResponse<CreateOrderCommand, CreateOrderCommand.Response> result = await carrotClient.SendReceiveAsync(
    new CreateOrderCommand { CustomerId = customerId, Lines = lines }
);
```

### Example: Asynchronous Fire-and-Monitor

```csharp
// Returns immediately; reply arrives on "order-reply-queue" later
await carrotClient.SendAsync(
    new CreateOrderCommand { CustomerId = customerId, Lines = lines },
    new QueueReplyEndPoint("order-reply-queue")
);
```
