using CarrotMQ.Core.Telemetry;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Configuration.Exchanges;
using CarrotMQ.RabbitMQ.Configuration.Queues;
using Dto;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Service1;

namespace Service2;

internal class Program
{
    public const string ResponseQueue = "my-response-queue";

    private static async Task Main(string[] args)
    {
        var appBuilder = new HostApplicationBuilder();

        appBuilder.Services.AddCarrotMqRabbitMq(
            builder =>
            {
                builder.ConfigureBrokerConnection(configureOptions: options => { options.ServiceName = "Service2"; });

                DirectExchangeBuilder exchange = builder.Exchanges.AddDirect<MyExchange>();

                QuorumQueueBuilder queue = builder.Queues.AddQuorum("My-queue-service2")
                    .WithConsumer(c => c.WithSingleAck());
                builder.Queues.AddClassic(ResponseQueue)
                    .WithConsumer(c => c.WithSingleAck());

                builder.Handlers.AddResponse<MyCommandResponseHandler, MyCommand, MyCommand.Response>();
                builder.Handlers.AddEvent<MyEventHandler, MyEvent>()
                    .BindTo(exchange, queue);

                builder.StartAsHostedService();
            });

        appBuilder.Services.AddHostedService<Service2BackgroundService>();

        appBuilder.Services.AddOpenTelemetry()
            .WithMetrics(
                metricsBuilder =>
                {
                    metricsBuilder.AddMeter("CarrotMQ.RabbitMQ.CarrotMeter");
                    metricsBuilder.AddRuntimeInstrumentation();
                })
            .WithTracing(tracingBuilder => { tracingBuilder.AddSource(Names.CarrotActivitySourceName); })
            .ConfigureResource(resource => resource.AddService("Service2"))
            .UseOtlpExporter();

        IHost host = appBuilder.Build();

        await host.RunAsync();
    }
}