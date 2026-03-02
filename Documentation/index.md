# CarrotMQ

CarrotMQ is a .NET library for building microservices over RabbitMQ with a clean, CQRS-inspired messaging model.
It is designed for teams that want strongly-typed, DI-friendly message passing without boilerplate, making it easy to
define events, commands, and queries as plain C# classes and wire them up to RabbitMQ exchanges and queues.

## Features

- **CQRS-inspired messaging** — define events, commands, and queries as plain C# classes
- **Strongly-typed message routing** — endpoints and message targets defined in C# types; no magic strings
- **Full .NET Dependency Injection integration** — register handlers and services with `IServiceCollection`
- **Async-first API** built on top of the official `RabbitMQ.Client`
- **Middleware pipeline** for cross-cutting concerns (logging, tracing, validation)
- **Publisher confirms** for at-least-once delivery guarantees
- **Built-in OpenTelemetry support** — distributed traces and metrics out of the box
- **Flexible topology** — quorum queues, classic queues, dead-letter exchanges, topic and fanout exchanges

## Packages

| Package | Description |
|---|---|
| **CarrotMQ.Core** | Transport-agnostic contracts: interfaces, base classes, and DTO types. Reference this in your shared DTO library. |
| **CarrotMQ.RabbitMQ** | RabbitMQ implementation. Reference this in your microservice and client projects. |

Both packages are available on [nuget.org](https://www.nuget.org).

## Getting Started

Follow the [Quick Start](docs/quick_start.md) guide to set up a minimal event publishing and consuming scenario in minutes.

## Learn More

- [Core Concepts](docs/concepts/overview.md) — architecture, message types, and the handler pipeline
- [Configuration Reference](docs/configuration/setup.md) — configure exchanges, queues, consumers, and connection settings
- [Observability](docs/observability/opentelemetry.md) — OpenTelemetry traces and metrics integration


