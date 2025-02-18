using CarrotMQ.Core.Tracing;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Configuration.Exchanges;
using CarrotMQ.RabbitMQ.Configuration.Queues;
using Dto;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace Service1;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var appBuilder = new HostApplicationBuilder();

        appBuilder.Services.AddCarrotMqRabbitMq(
            builder =>
            {
                builder.ConfigureBrokerConnection(configureOptions: options => { options.ServiceName = "Service1"; });

                DirectExchangeBuilder exchange = builder.Exchanges.AddDirect<MyExchange>();
                QuorumQueueBuilder queue = builder.Queues.AddQuorum<MyQueue>()
                    .WithConsumer(c => c.WithSingleAck());

               
                builder.Handlers.AddQuery<MyQueryHandler, MyQuery, MyQuery.Response>();
                builder.Handlers.AddCommand<MyCommandHandler, MyCommand, MyCommand.Response>();

                builder.StartAsHostedService();
            });

        appBuilder.Services.AddHostedService<Service1BackgroundService>();

        appBuilder.Services.AddOpenTelemetry()
            .WithMetrics(
                metricsBuilder =>
                {
                    metricsBuilder.AddMeter("CarrotMQ.RabbitMQ.CarrotMeter");
                    metricsBuilder.AddRuntimeInstrumentation();
                })
            .WithTracing(tracingBuilder => { tracingBuilder.AddSource(CarrotActivityFactory.TracingActivitySourceName); })
            .ConfigureResource(resource => resource.AddService("Service1"))
            .UseOtlpExporter();

        IHost host = appBuilder.Build();

        await host.RunAsync();
    }
}