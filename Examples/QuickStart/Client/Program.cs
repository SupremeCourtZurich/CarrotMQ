using CarrotMQ.Core;
using CarrotMQ.RabbitMQ.Configuration;
using Dto;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Client;

#region BootstrappingClient
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
            });

        IHost host = appBuilder.Build();

        await host.StartAsync().ConfigureAwait(false);

        // Get the configured ICarrotClient from the DI
        var carrotClient = host.Services.GetRequiredService<ICarrotClient>();

        // Publish an event over RabbitMQ
        await carrotClient.PublishAsync(new MyEvent { Message = "Hello World" }).ConfigureAwait(false);

        await host.StopAsync().ConfigureAwait(false);
    }
}
#endregion