## Quick Start 

For this quick start example, we will create 3 separate projects:

 - **Dto** project: Library project containing the event and endpoint definitions as C# classes.
 - **Microservice** project: Console application that will consume the events.
 - **Client** project: Console application that will publish the events.


```plantuml
rectangle Dto #B0E3E6 {
}

rectangle Client #D5E8D4 {
}

rectangle Microservice #DAE8FC {
}

rectangle "CarrotMQ<U+002E>Core" #E1D5E7 {
}

rectangle "CarrotMQ<U+002E>RabbitMQ" #E1D5E7 {
}

Dto -down-> "CarrotMQ<U+002E>Core"
Client -right-> Dto
Client -down-> "CarrotMQ<U+002E>RabbitMQ"
Microservice -left-> Dto
Microservice -down-> "CarrotMQ<U+002E>RabbitMQ"
"CarrotMQ<U+002E>RabbitMQ" -down-> "CarrotMQ<U+002E>Core"

```

### Dto Project
For the Dto project, create a .NET 9 Class Library project. Inside this project, we will define the endpoints and events.

This project should reference the **CarrotMQ.Core** nuget package.

#### Defining endpoints

[!code-csharp[](../../Examples/QuickStart/Dto/MyExchange.cs#MyExchangeDefinition)]

[!code-csharp[](../../Examples/QuickStart/Dto/MyQueue.cs#MyQueueDefinition)]

#### Defining an event

[!code-csharp[](../../Examples/QuickStart/Dto/MyEvent.cs#MyEventDefinition)]

### Microservice Project

For the microservice project, create a .NET 9 console project.

This project should reference the **CarrotMQ.RabbitMQ** nuget package and the **Dto** project.

#### Defining a handler

Inside the microservice project, create the handler class that will handle your event.

[!code-csharp[](../../Examples/QuickStart/Service/MyEventHandler.cs#MyEventHandlerDefinition)]

#### Bootstrapping the service

In the Program.cs of your microservice project, configure CarrotMQ for RabbitMQ.

[!code-csharp[](../../Examples/QuickStart/Service/Program.cs#BootstrappingService)]

### Client Project

For the client project, create a .NET 9 console project (this could be any project type you can run).

This project should also reference the **CarrotMQ.RabbitMQ** nuget package and the **Dto** project.

The configuration is similar to the microservice project without creating exchanges, queues, and registering consumers:

[!code-csharp[](../../Examples/QuickStart/Client/Program.cs#BootstrappingClient)]
Here we directly get the ICarrotClient out of the ServiceCollection, in a real application you can let the DI inject the ICarrotClient interface in your services.