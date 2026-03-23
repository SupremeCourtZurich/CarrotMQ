# AppSettings Reference

This page documents all CarrotMQ-related configuration sections that can be placed in `appsettings.json` (or any other `IConfiguration` source).

---

## Section Name Constants

| Options Class | Default Section Name | Constant |
|---|---|---|
| `BrokerConnectionOptions` | `"BrokerConnection"` | `BrokerConnectionOptions.BrokerConnection` |
| `CarrotTracingOptions` | `"CarrotTracing"` | `CarrotTracingOptions.CarrotTracing` |

Section names can be overridden when registering CarrotMQ:

```csharp
builder.ConfigureBrokerConnection(sectionName: "CustomSectionName");
builder.ConfigureTracing(sectionName: "CustomTracingSection");
```

---

## Complete Example

```json
{
  "BrokerConnection": {
    "BrokerEndPoints": ["amqp://localhost:5672"],
    "VHost": "/",
    "UserName": "guest",
    "Password": "guest",
    "ServiceName": "MyService",
    "ServiceInstanceId": "00000000-0000-0000-0000-000000000000",
    "DisplayConnectionName": null,
    "RandomizeEndPointResolving": true,
    "ConsumerDispatchConcurrency": 4,
    "InitialConnectionTimeout": "00:00:30",
    "PublisherConfirm": {
      "MaxConcurrentConfirms": 500,
      "RetryLimit": 100,
      "RetryIntervalInMs": 1000,
      "RepublishEvaluationIntervalInMs": 100
    }
  }
}
```

> **Note**: The `CarrotTracing` options only contain enrichment hooks (`EnrichPublishActivityWithHeader`, `EnrichConsumeActivityWithHeader`) which are delegate properties and **cannot be set from JSON** — configure them in code via `builder.ConfigureTracing(configureOptions: ...)`. See [OpenTelemetry](../observability/opentelemetry.md) for details.

---

## BrokerConnection Settings

| Property | Type | Required | Default | Description |
|---|---|---|---|---|
| `BrokerEndPoints` | `string[]` | ✅ | — | One or more RabbitMQ endpoint URIs (e.g. `amqp://localhost:5672`). CarrotMQ will attempt to connect to these in order (or randomly if `RandomizeEndPointResolving` is `true`). |
| `VHost` | `string` | ❌ | `""` | RabbitMQ virtual host to connect to. |
| `UserName` | `string` | ❌ | `""` | RabbitMQ username. |
| `Password` | `string` | ❌ | `""` | RabbitMQ password. |
| `ServiceName` | `string` | ✅ | — | Logical name of this service. Used in message headers and tracing. |
| `ServiceInstanceId` | `Guid` | ❌ | New GUID | Unique identifier for this service instance. Useful for correlating logs and traces across deployments. |
| `DisplayConnectionName` | `string?` | ❌ | `"{ServiceName} {ServiceInstanceId}"` | Optional human-readable name shown in the RabbitMQ Management UI for this connection. When `null`, defaults to a combination of `ServiceName` and `ServiceInstanceId`. |
| `RandomizeEndPointResolving` | `bool` | ❌ | `true` | When `true`, CarrotMQ randomly shuffles `BrokerEndPoints` before attempting to connect. Provides basic client-side load distribution across cluster nodes. |
| `ConsumerDispatchConcurrency` | `ushort` | ❌ | `4` | Number of concurrent consumer dispatch threads. Controls how many messages can be processed in parallel. |
| `InitialConnectionTimeout` | `TimeSpan` | ❌ | `Timeout.InfiniteTimeSpan` | How long to wait for the initial connection to the broker before throwing. The default (`InfiniteTimeSpan`) means the host will block until RabbitMQ becomes available. Specified as a `TimeSpan` string (`hh:mm:ss`) when set via JSON. |

### PublisherConfirm Settings

Nested under `BrokerConnection.PublisherConfirm`. See [Publisher Confirms](../configuration/publisher_confirms.md) for full details.

| Property | Type | Required | Default | Description |
|---|---|---|---|---|
| `MaxConcurrentConfirms` | `ushort` | ❌ | `500` | Maximum number of messages awaiting a broker confirm at any one time. Acts as a back-pressure limit. |
| `RetryLimit` | `ushort` | ❌ | `100` | Maximum number of retry attempts before throwing `RetryLimitExceededException`. |
| `RetryIntervalInMs` | `uint` | ❌ | `1000` | Milliseconds between retry attempts. |
| `RepublishEvaluationIntervalInMs` | `uint` | ❌ | `100` | How often (ms) CarrotMQ checks internally for messages that need to be republished. |

---

## CarrotTracing Settings

Nested under `CarrotTracing` (or your custom section name).

| Property | Type | Configurable from JSON | Description |
|---|---|---|---|
| `EnrichPublishActivityWithHeader` | `Action<Activity, CarrotHeader>` | ❌ | Delegate called when a publish span is created. Must be set in code. |
| `EnrichConsumeActivityWithHeader` | `Action<Activity, CarrotHeader>` | ❌ | Delegate called when a consume span is created. Must be set in code. |

Configure tracing enrichment in code:

```csharp
builder.ConfigureTracing(configureOptions: options =>
{
    options.EnrichPublishActivityWithHeader = (activity, header) =>
    {
        activity.SetTag("tenant.id", header.CustomHeader?.GetValueOrDefault("X-Tenant-Id"));
    };
});
```
