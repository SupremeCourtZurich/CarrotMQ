## CarrotMQ

[![CarrotMQ](https://github.com/SupremeCourtZurich/CarrotMQ/actions/workflows/main.yml/badge.svg?branch=main)](https://github.com/SupremeCourtZurich/CarrotMQ/actions/workflows/main.yml)

CarrotMQ is a .NET library simplifying microservice development with RabbitMQ. It allows to send and consume events, commands, and queries using simple .NET classes.

### Features

- Simplifies the development of microservices with RabbitMQ.
- Fully integrated with .NET dependency injection.
- Implements CQRS (Command Query Responsibility Segregation) enabling the clear separation of read and write operations.
- Define your commands, queries, events and their respective handlers using simple .NET classes.
- Async support.
- Uses [RabbitMQ.Client](https://github.com/rabbitmq/rabbitmq-dotnet-client) for RabbitMQ integration.
- Supports all .NET versions

### Components

- **CarrotMQ.Core** is a transport-agnostic library that defines the essential interfaces and base classes required for creating and handling messages using CarrotMQ.RabbitMQ.
- **CarrotMQ.RabbitMQ** provides the implementation for RabbitMQ, allowing you to integrate RabbitMQ messaging into your microservices.


## Documentation

* [Quick Start](https://SupremeCourtZurich.github.io/CarrotMQ/docs/quick_start.html)
* [XML Documentation](https://SupremeCourtZurich.github.io/CarrotMQ/xmlDoc/CarrotMQ.Core.html)

## Nuget Package

The nuget packages are available on nuget.org :

* [CarrotMQ.Core](https://www.nuget.org/packages/CarrotMQ.Core/)
* [CarrotMQ.RabbitMQ](https://www.nuget.org/packages/CarrotMQ.RabbitMQ/)


## About this project

CarrotMQ, initially developed in 2017 to support .NET Framework 4.7.2 microservices, has been significantly refactored and reimplemented. The project began under a different name, with the original concept and first version created by [JonLeDon](https://github.com/JonLeDon).  This initial version is still actively used in several internal applications and services at the Supreme Court of the Canton of Zürich (Switzerland).

Driven by the emergence of .NET Core and the improved asynchronous capabilities of the RabbitMQ.Client NuGet package, we undertook a substantial rewrite of CarrotMQ.

This new version is now open-sourced under the MIT license and shared with the community to encourage feedback, contributions, and collaborative development.  While we will continue to maintain and develop CarrotMQ for our own projects, external contributions and usage are highly welcomed.

### Contributors

A special thanks to the main contributors:

[<img src="https://github.com/JonLeDon.png?size=50" width="50" height="50">](https://github.com/JonLeDon)
[<img src="https://github.com/guido-frerker.png?size=50" width="50" height="50">](https://github.com/guido-frerker)
[<img src="https://github.com/adrian-moll.png?size=50" width="50" height="50">](https://github.com/adrian-moll)
[<img src="https://github.com/florinulrich.png?size=50" width="50" height="50">](https://github.com/florinulrich)
