## CarrotMQ

CarrotMQ is a powerful and versatile .NET library designed to streamline the development of microservices within a RabbitMQ-based architecture. It empowers developers to create efficient and robust microservices with ease, enabling you to define and communicate Events, Commands, and Queries using simple .NET classes.

### Components

- **CarrotMQ.Core**: is a transport-agnostic library that defines the essential interfaces and base classes required for creating and handling messages using CarrotMQ.RabbitMQ.
- **CarrotMQ.RabbitMQ**: Provides the implementation for RabbitMQ, allowing you to integrate RabbitMQ messaging into your microservices.

### Features

- Supports all .NET versions
- Simplifies the development of microservices with RabbitMQ.
- Implements CQRS (Command Query Responsibility Segregation) to enable the definition and communication of Events, Commands, and Queries.


## Documentation

* [Quick Start](https://SupremeCourtZurich.github.io/CarrotMQ/docs/quick_start.html)
* [XML Documentation](https://SupremeCourtZurich.github.io/CarrotMQ/xmlDoc/CarrotMQ.Core.html)

### Nuget Package

The nuget packages are available on nuget.org :

* [CarrotMQ.Core](https://www.nuget.org/packages/CarrotMQ.Core/)
* [CarrotMQ.RabbitMQ](https://www.nuget.org/packages/CarrotMQ.RabbitMQ/)