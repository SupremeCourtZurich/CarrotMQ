using CarrotMQ.Core.Dto;
using CarrotMQ.Core.EndPoints;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.RabbitMQ.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Documentation.CodeExamples;

#region EndPointDefinitions

public class MyExchange : ExchangeEndPoint
{
    public MyExchange() : base("my-exchange")
    {
    }
}

public class MyQueue : QueueEndPoint
{
    public MyQueue() : base("my-queue")
    {
    }
}

#endregion

#region EventDefinition

public class MyEvent : IEvent<MyEvent, MyExchange>
{
    public string Message { get; set; }
}

#endregion

#region HandlerDefinition

public class MyEventHandler : EventHandlerBase<MyEvent>
{
    private readonly ILogger<MyEventHandler> _logger;

    public MyEventHandler(ILogger<MyEventHandler> logger)
    {
        _logger = logger;
    }

    public override async Task<IHandlerResult> HandleAsync(MyEvent message, ConsumerContext consumerContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation(message.Message);

        await Task.Delay(100, cancellationToken).ConfigureAwait(false); // Do something with the event 

        return Ok();
    }
}

#endregion


#region BootstrappingService
internal class Program
{
    static async Task Main(string[] args)
    {
        var appBuilder = new HostApplicationBuilder();

        appBuilder.Services.AddCarrotMqRabbitMq(builder =>
        {
            builder.ConfigureBrokerConnection(configureOptions: options =>
            {
                // Configure the broker connection for your rabbitMq instance (can also be defined in the appsettings)
                options.UserName = "TestUser";
                options.Password = "MySuperPassword";
                options.BrokerEndPoints = [new Uri("amqp://localhost:5672")];
                options.VHost = "/";
                options.ServiceName = "CarrotMQ.Demo";
            });

            // Define the exchange and queue (will be created on startup)
            var exchange = builder.Exchanges.AddDirect<MyExchange>();
            var queue = builder.Queues.AddQuorum<MyQueue>()
                .WithConsumer(); // register a consumer

            // Register the event handler
            builder.Handlers.AddEvent<MyEventHandler, MyEvent>()
                .BindTo(exchange, queue); // Create the binding from the exchange to the queue with the event name as routing key

            // Start the service as hosted service --> this will start the consumers when starting the host and will continue until the service is stopped
            builder.StartAsHostedService();
        });

        var host = appBuilder.Build();


        await host.RunAsync().ConfigureAwait(false);

    }
}
#endregion