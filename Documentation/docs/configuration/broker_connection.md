# Broker Connection

`BrokerConnectionOptions` controls every aspect of how CarrotMQ connects to RabbitMQ: endpoints, credentials, TLS, and consumer threading.

Configure it via `builder.ConfigureBrokerConnection`:

```csharp
builder.ConfigureBrokerConnection(configureOptions: options =>
{
    options.BrokerEndPoints = [new Uri("amqp://localhost:5672")];
    options.UserName = "guest";
    options.Password = "guest";
    options.VHost = "/";
    options.ServiceName = "MyService";
});
```

Or load from a named configuration section:

```csharp
builder.ConfigureBrokerConnection(sectionName: "BrokerConnection");
```

---

## Property Reference

| Property | Type | Default | Description |
|---|---|---|---|
| `BrokerEndPoints` | `IList<Uri>` | `[]` | List of RabbitMQ node URIs (e.g. `amqp://host:5672`). |
| `RandomizeEndPointResolving` | `bool` | `true` | When `true`, a random endpoint from `BrokerEndPoints` is chosen on each connection attempt. Set to `false` to always try endpoints in order. |
| `VHost` | `string` | `""` | RabbitMQ virtual host to connect to. |
| `UserName` | `string` | `""` | AMQP authentication username. |
| `Password` | `string` | `""` | AMQP authentication password. |
| `ServiceName` | `string` | `""` | Logical name for this service. Appears in the RabbitMQ management UI and is included in outgoing message headers for tracing. |
| `ServiceInstanceId` | `Guid` | `Guid.NewGuid()` | Unique identifier for this running instance. Useful when multiple instances of the same service are connected simultaneously. |
| `DisplayConnectionName` | `string?` | `"{ServiceName} {ServiceInstanceId}"` | The connection name shown in the RabbitMQ management UI **Connections** tab. Defaults to a combination of `ServiceName` and `ServiceInstanceId`. |
| `ClientProperties` | `IDictionary<string, object?>` | `{}` | Arbitrary key-value pairs attached to the AMQP connection as client properties. Visible in the RabbitMQ management UI under connection details. |
| `InitialConnectionTimeout` | `TimeSpan` | `InfiniteTimeSpan` | How long `StartAsHostedService()` waits for the initial connection before throwing. The default (`InfiniteTimeSpan`) means the host will block until RabbitMQ becomes available. See [Connection Lifecycle](connection_lifecycle.md) for the full retry and shutdown behaviour. |
| `ConsumerDispatchConcurrency` | `ushort` | `4` | Number of threads used to dispatch incoming messages to consumer handlers. Set to `1` for strictly sequential processing; increase for higher throughput when handlers are independent. |
| `ConfigureConnectionFactory` | `Action<ConnectionFactory>?` | `null` | Advanced hook to configure the underlying `RabbitMQ.Client.ConnectionFactory` directly. Use this for TLS/SSL settings, custom socket options, or any option not exposed by `BrokerConnectionOptions`. |
| `PublisherConfirm` | `PublisherConfirmOptions` | *(see [Publisher Confirms](publisher_confirms.md))* | Controls publisher confirm behaviour (acknowledgement, timeouts, retry). |

---

## `BrokerEndPoints` and Failover

`BrokerEndPoints` accepts multiple URIs to support cluster failover:

```csharp
options.BrokerEndPoints =
[
    new Uri("amqp://rabbit1:5672"),
    new Uri("amqp://rabbit2:5672"),
    new Uri("amqp://rabbit3:5672"),
];
```

With `RandomizeEndPointResolving = true` (the default) CarrotMQ picks a random node on every reconnect attempt, spreading load across the cluster. Set it to `false` if you want deterministic failover — CarrotMQ will then try each endpoint in list order.

---

## `ConsumerDispatchConcurrency`

This value is passed directly to the `RabbitMQ.Client` channel factory.

- **`1`** — messages are processed one at a time on a single thread. Guarantees ordering but limits throughput.
- **`> 1`** — multiple handlers can run concurrently. Increases throughput but requires handlers to be thread-safe and idempotent.
- **`0`** — the value is not set on the underlying `ConnectionFactory`, which leaves it at the RabbitMQ.Client library default of `1`.

> **Tip:** Combine a higher `ConsumerDispatchConcurrency` with quorum queues and handler-level idempotency for robust, high-throughput consumers.

---

## `ConfigureConnectionFactory`

`ConfigureConnectionFactory` is an escape hatch that gives you direct access to the underlying `RabbitMQ.Client.ConnectionFactory` before the connection is established. Use it for any setting not exposed by `BrokerConnectionOptions`.

CarrotMQ sets `VirtualHost`, `UserName`, `Password`, `ClientProperties`, and `ConsumerDispatchConcurrency` on the factory before invoking your delegate, so you can override them or add additional settings without re-specifying the basics.

### TLS / SSL

```csharp
builder.ConfigureBrokerConnection(configureOptions: options =>
{
    options.BrokerEndPoints = [new Uri("amqps://myrabbit:5671")];
    options.ConfigureConnectionFactory = factory =>
    {
        factory.Ssl = new SslOption
        {
            Enabled = true,
            ServerName = "myrabbit",
            CertPath = "/path/to/cert.pfx",
            CertPassphrase = "password"
        };
    };
});
```

Note the `amqps://` scheme in the endpoint URI — this tells the client to use TLS on port 5671.

### Automatic Recovery Interval

The interval between automatic reconnect attempts is taken from `ConnectionFactory.NetworkRecoveryInterval`. To change it:

```csharp
options.ConfigureConnectionFactory = factory =>
{
    factory.NetworkRecoveryInterval = TimeSpan.FromSeconds(10);
};
```

### Disabling Automatic Recovery

Automatic recovery is enabled by default. To disable it (not recommended in production):

```csharp
options.ConfigureConnectionFactory = factory =>
{
    factory.AutomaticRecoveryEnabled = false;
};
```

> [!WARNING]
> Disabling automatic recovery means CarrotMQ will not reconnect after a connection loss. Only do this if you are managing reconnection externally.

---

## Loading from `appsettings.json`

All scalar properties of `BrokerConnectionOptions` can be bound from a configuration section:

```json
{
  "BrokerConnection": {
    "BrokerEndPoints": ["amqp://localhost:5672"],
    "UserName": "guest",
    "Password": "guest",
    "VHost": "/",
    "ServiceName": "MyService",
    "ConsumerDispatchConcurrency": 4
  }
}
```

Wire it up with:

```csharp
builder.ConfigureBrokerConnection(sectionName: "BrokerConnection");
```

The section name defaults to `"BrokerConnection"`, so passing no arguments is equivalent. You can also provide a `configureOptions` delegate alongside the section name to override individual values after binding — for example, to inject a secret from a vault at runtime:

```csharp
builder.ConfigureBrokerConnection(
    sectionName: "BrokerConnection",
    configureOptions: options =>
    {
        options.Password = secretsProvider.GetSecret("rabbitmq-password");
    });
```
