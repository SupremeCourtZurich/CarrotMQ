using System.Threading.Channels;
using CarrotMQ.Core;
using CarrotMQ.Core.Dto;
using CarrotMQ.Core.EndPoints;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Channel = System.Threading.Channels.Channel;

namespace CarrotMQ.RabbitMQ.Test.Integration;

[TestClass]
[TestCategory("Integration")]
public class PublisherRecoveryTest
{
    private const string QueueName = "test.publisher.recovery.queue";

    private readonly Channel<TransportErrorReceivedEventArgs.TransportErrorReason> _transportErrors =
        Channel.CreateBounded<TransportErrorReceivedEventArgs.TransportErrorReason>(10);
    private IBrokerConnection _brokerConnection = null!;
    private ICarrotClient _carrotClient = null!;
    private CarrotHelper _carrotHelper = null!;
    private ILogger _logger = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _carrotHelper = new CarrotHelper(
            "PublisherRecoveryTestConsumer",
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
                builder.StartAsHostedService();
            },
            services => { services.AddSingleton<ReceivedMessages>(); });

        _brokerConnection = _carrotHelper.Host.Services.GetRequiredService<IBrokerConnection>();
        _brokerConnection.TransportErrorReceived += BrokerConnection_TransportErrorReceived;
        _carrotClient = _carrotHelper.Host.Services.GetRequiredService<ICarrotClient>();
        await _carrotHelper.WaitForConsumerHostBootstrapToCompleteAsync();

        var loggerFactory = _carrotHelper.Host.Services.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger("PublisherRecoveryTest");
    }

    [TestCleanup]
    public void Cleanup()
    {
        _brokerConnection.TransportErrorReceived -= BrokerConnection_TransportErrorReceived;
        _carrotHelper.Dispose();
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task PublisherRecoveryAfterPublishToFalseExchange()
    {
        // Publish 3 valid events
        var p1 = _carrotClient.PublishAsync(new TestEvent { Id = 101 }, cancellationToken: CancellationToken.None);
        var p2 = _carrotClient.PublishAsync(new TestEvent { Id = 102 }, cancellationToken: CancellationToken.None);
        var p3 = _carrotClient.PublishAsync(new TestEvent { Id = 103 }, cancellationToken: CancellationToken.None);

        // Publish 1 invalid event (non-existent exchange)
        using var ctx = new CancellationTokenSource();
        var p4 = _carrotClient.PublishAsync(new InvalidTestEvent { Id = 104 }, cancellationToken: ctx.Token);

        // Wait for channel interruption (waiting on TransportError)
        var transportErrorReason = await _transportErrors.Reader.ReadAsync(CancellationToken.None);
        Assert.AreEqual(TransportErrorReceivedEventArgs.TransportErrorReason.ChannelInterrupted, transportErrorReason);

        // Publish 3 valid events
        var p5 = _carrotClient.PublishAsync(new TestEvent { Id = 105 }, cancellationToken: CancellationToken.None);
        var p6 = _carrotClient.PublishAsync(new TestEvent { Id = 106 }, cancellationToken: CancellationToken.None);
        var p7 = _carrotClient.PublishAsync(new TestEvent { Id = 107 }, cancellationToken: CancellationToken.None);

        // Cancel and wait for the "invalid" event to be cancelled and emitted as TransportError
#if NET
        await ctx.CancelAsync().ConfigureAwait(false);
#else
        ctx.Cancel();
#endif

        await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => p4).ConfigureAwait(false);

        var allMessagesReceived = await AwaitDistinctMessagesAsync(6, CancellationToken.None).ConfigureAwait(false);
        Assert.IsTrue(allMessagesReceived, nameof(AwaitDistinctMessagesAsync));

        _logger.LogInformation("Check if all PublishAsync are done.");
        await Task.WhenAll(p1, p2, p3, p5, p6, p7).ConfigureAwait(false);
    }

    private async void BrokerConnection_TransportErrorReceived(object? sender, TransportErrorReceivedEventArgs e)
    {
        await _transportErrors.Writer.WriteAsync(e.ErrorReason).ConfigureAwait(false);
    }

    private async Task<bool> AwaitDistinctMessagesAsync(int numberOfMessages, CancellationToken token)
    {
        HashSet<int> distinctIds = [];
        try
        {
            var receivedMessages = _carrotHelper.Host.Services.GetRequiredService<ReceivedMessages>();
            while (!token.IsCancellationRequested)
            {
                var receivedId = await receivedMessages.ReadAsync(token).ConfigureAwait(false);
                if (distinctIds.Contains(receivedId))
                {
                    _logger.LogInformation("Duplicate id received {Id}", receivedId);
                }
                else
                {
                    _logger.LogInformation("Add new id {Id}", receivedId);
                    distinctIds.Add(receivedId);

                    if (distinctIds.Count == numberOfMessages)
                    {
                        return true;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // ignore
        }

        return false;
    }

    public class NonExistentExchange : ExchangeEndPoint
    {
        public NonExistentExchange() : base("test.nonexistent.exchange")
        {
        }
    }

    public class InvalidTestEvent : IEvent<InvalidTestEvent, NonExistentExchange>
    {
        public int Id { get; set; }
    }
}