using CarrotMQ.Core.Tracing;
using CarrotMQ.RabbitMQ.Configuration;
using Dto;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace Client;

internal class Program
{
    public const string ClientQueue = "my-client-queue";
    public const string ClientExchange = "my-client-exchange";
    public static Guid CustomRoutingKey = Guid.NewGuid();
    private static async Task Main(string[] args)
    {
        var appBuilder = new HostApplicationBuilder();

        appBuilder.Services.AddCarrotMqRabbitMq(
            builder =>
            {
                builder.ConfigureBrokerConnection(configureOptions: options =>
                {
                    options.ServiceName = "Client1"; 
                });


                var clientQueue = builder.Queues.AddClassic(ClientQueue)
                    .WithAutoDelete()
                    .WithConsumer(c => c.WithSingleAck());

                builder.Exchanges.AddDirect(ClientExchange)
                    .BindToQueue(clientQueue, CustomRoutingKey.ToString());


                builder.Handlers.AddResponseSubscription<MyQuery, MyQuery.Response>();
                builder.Handlers.AddCustomRoutingEventSubscription<MyCustomRoutingEvent>();

                builder.StartAsHostedService();
            });

        appBuilder.Services.AddHostedService<ClientBackgroundService>();

        appBuilder.Services.AddOpenTelemetry()
            .WithMetrics(
                metricsBuilder =>
                {
                    metricsBuilder.AddMeter("CarrotMQ.RabbitMQ.CarrotMeter");
                    metricsBuilder.AddRuntimeInstrumentation();
                })
            .WithTracing(tracingBuilder => { tracingBuilder.AddSource(CarrotActivityFactory.TracingActivitySourceName); })
            .ConfigureResource(resource => resource.AddService("Client1"))
            .UseOtlpExporter();

        IHost host = appBuilder.Build();

        await host.RunAsync();
    }
}