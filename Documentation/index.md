## Carrot

Welcome to the Carrot documentation. This document provides an overview of the core types and functionalities included
in the Carrot.Core NuGet package, which serves as the foundation for our messaging client library.

## Overview

*Carrot.Core* is a transport-agnostic library that defines the essential interfaces and base classes required for
creating and handling messages within a messaging system. This package is designed to be flexible and extensible,
allowing developers to build robust and maintainable messaging solutions.

Installation
To install Carrot.Core, run the following command in your project:

```sh
dotnet add package Carrot.Core
```

&NewLine;

*Carrot.RabbitMq* is the implementation of the RabbitMQ Transport for Carrot.Core.

```sh
dotnet add package Carrot.RabbitMq
```

## Usage

### Defining EndPoints

```plantuml


package "Carrot Core" {
  abstract class EndPointBase {
    # EndPointBase(exchangeName:string)
    + Exchange : string <<get>>
  }
  abstract class ExchangeEndPoint {
    # ExchangeEndPoint(exchangeName:string)
  }
  EndPointBase <|-- ExchangeEndPoint
  abstract class QueueEndPoint {
      # QueueEndPoint(queueName:string)
      + QueueName : string <<get>>
  }
  EndPointBase <|-- QueueEndPoint
}

package "YourDto"{

  together{
    class MyExchange{
      {static} ExchangeNameInternal : string <<const>>
    }
    ExchangeEndPoint <|-- MyExchange
    class MyQueue{
      {static} QueueNameInternal : string <<const>>
    }
    QueueEndPoint <|-- MyQueue
  }
}

```

&NewLine;

```csharp
public class MyExchange : ExchangeEndPoint
{
    private const string ExchangeNameInternal = "my-exchange.exchange";

    public MyExchange() : base(ExchangeName)
    {
    }
}

public class MyQueue : QueueEndPoint
{
    private const string QueueNameInternal = "my-queue.queue";

    public MyQueue() : base(QueueNameInternal)
    {
    }
}

```

### Definig Messages

```plantuml

package "Carrot Core"{
  
  interface "IEvent`2"<TEvent,TExchangeEndPoint>
  interface "IQuery`3"<TQuery,TResponse,TEndPointDefinition> 
  interface "ICommand`3"<TCommand,TResponse,TEndPointDefinition>
  interface "ICustomRoutingEvent`1"<TEvent> {
    + Exchange : string <<get>> <<set>>
    + RoutingKey : string <<get>> <<set>>
  }

    
   "ICustomRoutingEvent`1" -[hidden]down- "IQuery`3"
   

}

package "YourDto"{
    
    class MyEvent
  
    "IEvent`2" "<MyEvent, MyExchange>" <|-- MyEvent
  
    class MyCreateCmd
    class MyResponse
  
    "ICommand`3" "<MyCreateCmd,MyResponse,MyQueue>"<|-- MyCreateCmd
  
  
    class MyQuery
    class MyQueryResponse
  
    "IQuery`3" "<MyQuery,MyQueryResponse,MyQueue>"<|-- MyQuery

}

```

&NewLine;

- [ICommand<TCommand, TResponse, TEndPointDefinition>](xref:Carrot.Core.Dto.ICommand`3): Represents a command message.

- [ICustomRoutingEvent<TEvent>](xref:Carrot.Core.Dto.ICustomRoutingEvent`1): Represents an event message with custom
  routing logic.

- [IEvent<TEvent, TExchangeEndPoint>](xref:Carrot.Core.Dto.IEvent`2): Represents a standard event message.

- [IQuery<TQuery, TResponse, TEndPointDefinition>](xref:Carrot.Core.Dto.IQuery`3): Represents a query message.

&NewLine;

```csharp
public class MyCommand : ICommand<MyCommand, MyCommand.MyResponse, MyExchange>
{
    public required string CommandData { get; init; }

    public class MyResponse
    {
        publis bool Success { get; set; }
    }
}

public class MyEvent : IEvent<MyEvent, MyExchange>
{
    public required string EventData { get; init; }
}

public class MyQuery : IQuery<MyQuery, MyQuery.MyQueryResponse, MyQueue>
{
    public required string QueryData { get; init; }

    public class MyQueryResponse
    {
        publis bool Success { get; set; }
    }
}

```

### Defining Handlers

```plantuml

package "Carrot Core"{
  abstract class "HandlerBase`2"<TMessage,TResponse> {
    + {abstract} HandleAsync(TMessage, ConsumerContext, CancellationToken) : Task<HandlerResult>
  }
  abstract class "EventHandlerBase`1"<TEvent>
  "HandlerBase`2" "<TEvent,NoResponse>" <|-- "EventHandlerBase`1"

  abstract class "RequestHandlerBase`2"<TRequest,TResponse>
  "HandlerBase`2" "<TRequest,TResponse>" <|-- "RequestHandlerBase`2"
  "RequestHandlerBase`2" "<TCommand,TResponse>" <|-- "CommandHandlerBase`2"
  "RequestHandlerBase`2" "<TQuery,TResponse>" <|-- "QueryHandlerBase`2"
  abstract class "QueryHandlerBase`2"<TQuery,TResponse>
  abstract class "CommandHandlerBase`2"<TCommand,TResponse>
  
  abstract class "ResponseHandlerBase`2"<TRequest,TResponse>
  "HandlerBase`2" "<CarrotResponse<TRequest, TResponse>,NoResponse>" <|-- "ResponseHandlerBase`2"
    
}


package "YourService"{
  "EventHandlerBase`1" "<MyEvent>" <|-- MyEventHandler
  "CommandHandlerBase`2" "<MyCreateCmd,MyResponse>" <|-- MyCreateCmdHandler
  "QueryHandlerBase`2" "<MyQuery,MyQueryResponse>" <|-- MyQueryCmdHandler
}

```

&NewLine;

- [EventHandlerBase<TEvent>](xref:Carrot.Core.Handlers.EventHandlerBase`1): A base class for handling event messages.
- [QueryHandlerBase<TQuery, TResponse>](xref:Carrot.Core.Handlers.QueryHandlerBase`2): A base class for handling query
  messages.
- [CommandHandlerBase<TCommand, TResponse>](xref:Carrot.Core.Handlers.CommandHandlerBase`2): A base class for handling
  command messages.
- [ResponseHandlerBase<TRequest, TResponse>](xref:Carrot.Core.Handlers.ResponseHandlerBase`2): A base class for handling
  response messages.

&NewLine;

```csharp
public sealed class MyCreateCmdHandler
    : CommandHandlerBase<MyCreateCmd, MyCreateCmd.MyResponse>
{
    public override async Task<HandlerResult> HandleAsync(
        MyCreateCmd cmd,
        ConsumerContext context,
        CancellationToken cancellationToken)
    {
        // Do something

        return Ok(new MyCreateCmd.MyResponse());
    }

}


public sealed class MyQueryCmdHandler
    : CommandHandlerBase<MyQuery, MyQuery.MyQueryResponse>
{
    public override async Task<HandlerResult> HandleAsync(
        MyQuery query,
        ConsumerContext context,
        CancellationToken cancellationToken)
    {
        // Do something

        return Ok(new MyQuery.MyQueryResponse());
    }

}

public sealed class MyEventHandler
    : EventHandlerBase<MyEvent>
{
    public override async Task<HandlerResult> HandleAsync(
        MyEvent @event,
        ConsumerContext context,
        CancellationToken cancellationToken)
    {
        // Do something

        return Ok();
    }

}

```