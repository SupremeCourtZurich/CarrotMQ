using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Configuration.Exchanges;
using CarrotMQ.RabbitMQ.Configuration.Queues;
using Dto;
using Microsoft.Extensions.Hosting;

namespace Service;

#region BootstrappingService
internal class Program
{
    private static async Task Main(string[] args)
    {
        var appBuilder = new HostApplicationBuilder();

        appBuilder.Services.AddCarrotMqRabbitMq(
            builder =>
            {
                builder.ConfigureBrokerConnection(
                    configureOptions: options =>
                    {
                        // Configure the broker connection for your rabbitMq instance (can also be defined in the appsettings)
                        options.UserName = "TestUser";
                        options.Password = "MySuperPassword";
                        options.BrokerEndPoints = [new Uri("amqp://localhost:5672")];
                        options.VHost = "/";
                        options.ServiceName = "CarrotMQ.Demo";
                    });

                // Define the exchange and queue (will be created on startup)
                DirectExchangeBuilder exchange = builder.Exchanges.AddDirect<MyExchange>();
                QuorumQueueBuilder queue = builder.Queues.AddQuorum<MyQueue>()
                    .WithConsumer(); // register a consumer

                // Register the event handler
                builder.Handlers.AddEvent<MyEventHandler, MyEvent>()
                    .BindTo(exchange, queue); // Create the binding from the exchange to the queue with the event name as routing key

                // Start the service as hosted service --> this will start the consumers when starting the host and will continue until the service is stopped
                builder.StartAsHostedService();
            });

        IHost host = appBuilder.Build();

        await host.RunAsync().ConfigureAwait(false);
    }
}
#endregion