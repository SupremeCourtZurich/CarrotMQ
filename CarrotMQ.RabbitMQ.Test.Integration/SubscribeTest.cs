using CarrotMQ.Core;
using CarrotMQ.Core.Dto;
using CarrotMQ.Core.EndPoints;
using CarrotMQ.Core.Handlers;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;
using Microsoft.Extensions.DependencyInjection;
using Channel = System.Threading.Channels.Channel;

namespace CarrotMQ.RabbitMQ.Test.Integration;

[TestClass]
[TestCategory("Integration")]
public class SubscribeTest
{
    private const string QueueName = "test.subscribed.consumer.queue";
    private const string CustomRoutingKey = "custom-routing-key";
    private const string ResponseRoutingKey = "response-routing-key";

    private static CarrotHelper s_hostHelper = null!;
    private static ICarrotClient s_client = null!;
    private static ICarrotConsumerManager s_carrotConsumerManager = null!;
    private static EventSubscription<TestEvent> s_testEventSubscription = null!;
    private static EventSubscription<CustomRoutingEvent> s_testCustomEventSubscription = null!;
    private static ResponseSubscription<TestQuery, TestQuery.Response> s_testResponseSubscription = null!;

    private CancellationTokenSource _cts = null!;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        s_hostHelper = new CarrotHelper(
            "SubscribeTest",
            builder =>
            {
                var exchange = builder.Exchanges.AddDirect<TestExchange>();

                var queue = builder.Queues.AddQuorum(QueueName)
                    .WithConsumer(
                        c => c
                            .WithPrefetchCount(0)
                            .WithSingleAck());

                builder.Handlers.AddQuery<TestQueryHandler, TestQuery, TestQuery.Response>()
                    .BindTo(exchange, queue);

                builder.Handlers.AddEventSubscription<TestEvent>()
                    .BindTo(exchange, queue);

                builder.Handlers.AddCustomRoutingEventSubscription<CustomRoutingEvent>();
                exchange.BindToQueue(queue, CustomRoutingKey);

                builder.Handlers.AddResponseSubscription<TestQuery, TestQuery.Response>();
                exchange.BindToQueue(queue, ResponseRoutingKey);
            });

        s_client = s_hostHelper.Host.Services.GetRequiredService<ICarrotClient>();
        s_carrotConsumerManager = s_hostHelper.Host.Services.GetRequiredService<ICarrotConsumerManager>();
        await s_carrotConsumerManager.StartConsumingAsync();

        s_testEventSubscription = s_hostHelper.Host.Services.GetRequiredService<EventSubscription<TestEvent>>();
        s_testCustomEventSubscription = s_hostHelper.Host.Services.GetRequiredService<EventSubscription<CustomRoutingEvent>>();
        s_testResponseSubscription = s_hostHelper.Host.Services.GetRequiredService<ResponseSubscription<TestQuery, TestQuery.Response>>();
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        s_hostHelper.Dispose();
    }

    [TestInitialize]
    public void Initialize()
    {
        _cts = new CancellationTokenSource(1_000);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _cts.Dispose();
    }

    [TestMethod]
    public async Task SubscribeToEvent()
    {
        const int id = 1;
        var channel = Channel.CreateBounded<int>(10);
        s_testEventSubscription.EventReceived += async (_, args) =>
        {
            Console.WriteLine("Message received: " + args.Event.Id);
            await channel.Writer.WriteAsync(args.Event.Id).ConfigureAwait(false);
        };

        await s_client.PublishAsync(new TestEvent { Id = id }, cancellationToken: _cts.Token);

        var receivedId = await channel.Reader.ReadAsync(_cts.Token);
        Assert.AreEqual(id, receivedId);
    }

    [TestMethod]
    public async Task SubscribeToCustomRoutingEvent()
    {
        // Arrange
        var channel = Channel.CreateBounded<int>(10);
        const int id = 2;
        s_testCustomEventSubscription.EventReceived += async (_, args) =>
        {
            Console.WriteLine("Message received: " + args.Event.Id);
            await channel.Writer.WriteAsync(args.Event.Id).ConfigureAwait(false);
        };

        // Act
        await s_client.PublishAsync(new CustomRoutingEvent(TestExchange.Name, CustomRoutingKey) { Id = id }, cancellationToken: _cts.Token);

        // Assert
        var receivedId = await channel.Reader.ReadAsync(_cts.Token);
        Assert.AreEqual(id, receivedId);
    }

    [TestMethod]
    public async Task SubscribeToQueryResponse()
    {
        // Arrange
        var channel = Channel.CreateBounded<CarrotResponse<TestQuery, TestQuery.Response>>(10);
        const int id = 4;

        s_testResponseSubscription.ResponseReceived += async (_, args) =>
        {
            Console.WriteLine("Message received: " + args.Response.Content?.Id);
            await channel.Writer.WriteAsync(args.Response).ConfigureAwait(false);
        };

        // Act
        var responseEndPoint = new ExchangeReplyEndPoint(TestExchange.Name, CustomRoutingKey, true);
        await s_client.SendAsync(new TestQuery { Id = id }, responseEndPoint, cancellationToken: _cts.Token);

        // Assert
        var carrotResponse = await channel.Reader.ReadAsync(_cts.Token);
        Assert.AreEqual(id, carrotResponse.Content?.Id, "Response.Id");
        Assert.AreEqual(id, carrotResponse.Request?.Id, "OriginalRequest.Id");
    }

    public class CustomRoutingEvent : ICustomRoutingEvent<CustomRoutingEvent>
    {
        public CustomRoutingEvent(string exchange, string routingKey)
        {
            Exchange = exchange;
            RoutingKey = routingKey;
        }

        public int Id { get; set; }

        public string Exchange { get; set; }

        public string RoutingKey { get; set; }
    }
}