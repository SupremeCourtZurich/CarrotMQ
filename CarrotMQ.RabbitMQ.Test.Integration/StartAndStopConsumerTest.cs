using CarrotMQ.Core;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace CarrotMQ.RabbitMQ.Test.Integration;

[TestClass]
[TestCategory("Integration")]
public class StartAndStopConsumerTest
{
    private const string QueueName = "test.start.stop.consumer.queue";
    private ICarrotConsumerManager _carrotConsumerManager = null!;

    private CarrotHelper _carrotHelper = null!;
    private ICarrotClient _client = null!;
    private CancellationTokenSource _cts = null!;

    [TestInitialize]
    public void Initialize()
    {
        _carrotHelper = new CarrotHelper(
            $"{nameof(StartAndStopConsumerTest)}",
            builder =>
            {
                var exchange = builder.Exchanges.AddDirect<TestExchange>();

                var queue = builder.Queues.AddQuorum(QueueName)
                    .WithConsumer(
                        c => c
                            .WithPrefetchCount(0)
                            .WithSingleAck());

                builder.Handlers.AddEvent<TestEventHandler, TestEvent>()
                    .BindTo(exchange, queue);
            },
            services => { services.AddSingleton<ReceivedMessages>(); });

        _client = _carrotHelper.Host.Services.GetRequiredService<ICarrotClient>();
        _carrotConsumerManager = _carrotHelper.Host.Services.GetRequiredService<ICarrotConsumerManager>();
        _cts = new CancellationTokenSource(60_000);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _cts.Dispose();
        _carrotHelper.Dispose();
    }

    /// <summary>
    /// Validate that the <see cref="ConsumerChannel.StopConsumingAsync" />  really stops the consumption of messages
    /// </summary>
    [TestMethod]
    public async Task Start_And_Stop_Consuming()
    {
        var receivedMessages = _carrotHelper.Host.Services.GetRequiredService<ReceivedMessages>();
        await _carrotConsumerManager.StartConsumingAsync();

        await _client.PublishAsync(new TestEvent { Id = 111 }, cancellationToken: _cts.Token);
        await _client.PublishAsync(new TestEvent { Id = 112 }, cancellationToken: _cts.Token);

        var receivedId = await receivedMessages.ReadAsync(_cts.Token);
        Assert.AreEqual(111, receivedId);
        receivedId = await receivedMessages.ReadAsync(_cts.Token);
        Assert.AreEqual(112, receivedId);

        await _carrotConsumerManager.StopConsumingAsync();

        await _client.PublishAsync(new TestEvent { Id = 113 }, cancellationToken: _cts.Token);
        await _client.PublishAsync(new TestEvent { Id = 114 }, cancellationToken: _cts.Token);

        await TestBase.RabbitApi.AwaitNumberOfMessagesInQueueAsync(QueueName, 2, _cts.Token);

        await _carrotConsumerManager.StartConsumingAsync();

        receivedId = await receivedMessages.ReadAsync(_cts.Token);
        Assert.AreEqual(113, receivedId);
        receivedId = await receivedMessages.ReadAsync(_cts.Token);
        Assert.AreEqual(114, receivedId);
    }
}