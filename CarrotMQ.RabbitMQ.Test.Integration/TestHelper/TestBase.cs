using CarrotMQ.Core;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.RabbitMq;
using TestExchange = CarrotMQ.RabbitMQ.Test.Integration.Handlers.TestExchange;

namespace CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

[TestClass]
public class TestBase
{
    public const string DeadLetterExchange = "test.dead.letter.exchange";
    public const string DeadLetterQueue = "test.dead.letter.queue";

    internal static ICarrotClient CarrotClient = null!;
    internal static CarrotHelper ProducerHost = null!;
    internal static CarrotHelper ConsumerHost = null!;
    internal static RabbitApi RabbitApi = null!;
    internal static ReceivedMessages ReceivedMessages = null!;
    internal static ReceivedResponses ReceivedResponses = null!;
    internal static DeadLetterConsumer DeadLetterConsumer = null!;
    internal static BarrierBag BarrierBag = null!;

    private static RabbitMqContainer s_rabbitContainer = null!;

    protected CancellationTokenSource Cts = null!;

    public static string RabbitMqConnectionString { get; private set; } = null!;

    public static string RabbitMqContainerPassword { get; } = "rmqTestUser1233";

    public static string RabbitMqContainerUsername { get; } = "rmqTestUser";

    public static int RabbitMqContainerAmqpPort { get; private set; }

    public static int RabbitMqContainerHttpPort { get; private set; }

    public static string RabbitMqVhost { get; private set; } = null!;

    [AssemblyInitialize]
    public static async Task AssemblyInitialize(TestContext context)
    {
        s_rabbitContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:4.0-management")
            .WithUsername(RabbitMqContainerUsername)
            .WithPassword(RabbitMqContainerPassword)
            .WithExposedPort(5672)
            .WithPortBinding(15672, true)
            .Build();

        await s_rabbitContainer.StartAsync();

        RabbitMqContainerAmqpPort = s_rabbitContainer.GetMappedPublicPort(5672);
        RabbitMqContainerHttpPort = s_rabbitContainer.GetMappedPublicPort(15672);

        RabbitMqConnectionString = $"amqp://127.0.0.1:{RabbitMqContainerAmqpPort}";
        RabbitMqVhost = $"vhost-test-{Guid.NewGuid()}";

        var brokerConnectionOptions = ConfigureBroker();

        RabbitApi = new RabbitApi(brokerConnectionOptions, RabbitMqContainerHttpPort);
        await RabbitApi.CreateVHostAsync().ConfigureAwait(false);

        ProducerHost = new CarrotHelper("Test.Integration.Producer");
        CarrotClient = ProducerHost.Host.Services.GetRequiredService<ICarrotClient>();

        ConsumerHost = SetupConsumerHost();
        ReceivedMessages = ConsumerHost.Host.Services.GetRequiredService<ReceivedMessages>();
        ReceivedResponses = ConsumerHost.Host.Services.GetRequiredService<ReceivedResponses>();
        DeadLetterConsumer = ConsumerHost.Host.Services.GetRequiredService<DeadLetterConsumer>();
        BarrierBag = ConsumerHost.Host.Services.GetRequiredService<BarrierBag>();
        await ConsumerHost.WaitForConsumerHostBootstrapToCompleteAsync().ConfigureAwait(false);
        await DeadLetterConsumer.InitializeAsync(DeadLetterQueue, DeadLetterExchange).ConfigureAwait(false);
    }

    [AssemblyCleanup]
    public static async Task AssemblyCleanup()
    {
        ConsumerHost.Dispose();
        ProducerHost.Dispose();
        await RabbitApi.DeleteVHostAsync().ConfigureAwait(false);
        RabbitApi.Dispose();

        await s_rabbitContainer.DisposeAsync().ConfigureAwait(false);
    }

    [TestInitialize]
    public void Initialize()
    {
        Cts = new CancellationTokenSource(30_000);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Cts.Cancel();
        Cts.Dispose();
    }

    public static BrokerConnectionOptions ConfigureBroker(BrokerConnectionOptions? options = null)
    {
        options ??= new BrokerConnectionOptions();
        options.Password = RabbitMqContainerPassword;
        options.UserName = RabbitMqContainerUsername;
        options.BrokerEndPoints = [new Uri(RabbitMqConnectionString)];
        options.VHost = RabbitMqVhost;
        options.ServiceName = "CarrotMQ.RabbitMQ.Test.Integration";
        options.ConsumerDispatchConcurrency = 1;
        return options;
    }

    private static CarrotHelper SetupConsumerHost()
    {
        var consumerHost = new CarrotHelper(
            "Test.Integration.Consumer",
            builder =>
            {
                builder.ConfigureBrokerConnection(
                    configureOptions: options =>
                    {
                        ConfigureBroker(options);
                        options.InitialConnectionTimeout = TimeSpan.FromSeconds(5);
                        options.ConsumerDispatchConcurrency = 20;
                    });

                builder.Exchanges.AddFanOut(DeadLetterExchange);

                var exchange = builder.Exchanges.AddDirect<TestExchange>();

                var queue1 = builder.Queues.AddQuorum<TestQueue>()
                    .WithDeadLetterExchange(DeadLetterExchange)
                    .WithConsumer(
                        c => c.WithPrefetchCount(0)
                            .WithSingleAck())
                    .WithDeliveryLimit(3);

                builder.Handlers.AddCommand<ExchangeEndPointCmdHandler, ExchangeEndPointCmd, ExchangeEndPointCmd.Response>()
                    .BindTo(exchange, queue1);
                builder.Handlers
                    .AddCommand<ExchangeEndPointGenericResponseCmdHandler, ExchangeEndPointGenericResponseCmd,
                        ExchangeEndPointGenericResponseCmd.GenericResponse<ExchangeEndPointGenericResponseCmd.Response>>()
                    .BindTo(exchange, queue1);

                builder
                    .Handlers.AddQuery<ExchangeEndPointQueryHandler, ExchangeEndPointQuery, ExchangeEndPointQuery.Response>()
                    .BindTo(exchange, queue1);
                builder.Handlers.AddEvent<ExchangeEndPointEventHandler, ExchangeEndPointEvent>().BindTo(exchange, queue1);
                builder.Handlers.AddCustomRoutingEvent<ExchangeEndPointCustomRoutingEventHandler, ExchangeEndPointCustomRoutingEvent>();

                exchange.BindToQueue(queue1, ExchangeEndPointCustomRoutingEvent.GetRoutingKey());

                builder.Handlers.AddCommand<QueueEndPointCmdHandler, QueueEndPointCmd, QueueEndPointCmd.Response>();

                builder.Handlers.AddQuery<QueueEndPointQueryHandler, QueueEndPointQuery, QueueEndPointQuery.Response>();

                builder.Handlers.AddResponse<QueueEndPointQueryResponseHandler, QueueEndPointQuery, QueueEndPointQuery.Response>();
                exchange.BindToQueue(queue1, QueueEndPointQuery.Response.GetRoutingKey());

                builder
                    .Handlers.AddResponse<QueueEndPointCmdResponseHandler, QueueEndPointCmd, QueueEndPointCmd.Response>();
                exchange.BindToQueue(queue1, QueueEndPointCmd.Response.GetRoutingKey());

                builder
                    .Handlers.AddResponse<ExchangeEndPointCmdResponseHandler, ExchangeEndPointCmd, ExchangeEndPointCmd.Response>();
                exchange.BindToQueue(queue1, ExchangeEndPointCmd.Response.GetRoutingKey());

                builder
                    .Handlers.AddResponse<ExchangeEndPointQueryResponseHandler, ExchangeEndPointQuery, ExchangeEndPointQuery.Response>();
                exchange.BindToQueue(queue1, ExchangeEndPointQuery.Response.GetRoutingKey());

                builder.StartAsHostedService();
            },
            services =>
            {
                services.AddSingleton<ReceivedMessages>();
                services.AddSingleton<ReceivedResponses>();
                services.AddSingleton<DeadLetterConsumer>();
                services.AddSingleton<BarrierBag>();
            });

        return consumerHost;
    }
}