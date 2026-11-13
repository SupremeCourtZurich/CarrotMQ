using System.Collections.Concurrent;
using CarrotMQ.Core;
using CarrotMQ.Core.Dto;
using CarrotMQ.Core.EndPoints;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Serialization;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarrotMQ.RabbitMQ.Test.Integration;

/// <summary>
/// A publish the broker refuses must not cost the other messages waiting for a confirm.
/// </summary>
/// <remarks>
/// The producer publishes strictly in order through a <see cref="ChannelSpy" />; the consumer runs the handlers.
/// </remarks>
[TestClass]
[TestCategory("Integration")]
public class PublishRefusalTest
{
    private const ushort RetryLimit = 5;
    private const string QueueName = "test.publish.refusal.queue";

    // Always exists; with an unbound routing key the broker confirms at once.
    private const string ExistingExchange = "amq.direct";
    private const string MissingExchange = "test.publish.refusal.missing.exchange";
    private const string LateExchange = "test.publish.refusal.late.exchange";

    private readonly LogRecorder _producerLog = new();
    private readonly ChannelSpy _spy = new();
    private ICarrotClient _client = null!;
    private CarrotHelper _consumer = null!;
    private CarrotHelper _producer = null!;
    private ITransport _transport = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        RefusalTestQueryHandler.Reset();
        await TestBase.RabbitApi.DeleteExchangeAsync(LateExchange).ConfigureAwait(false);
        _consumer = CreateConsumer();
        _producer = CreateProducer();
        _client = _producer.Host.Services.GetRequiredService<ICarrotClient>();
        _transport = _producer.Host.Services.GetRequiredService<ITransport>();
        await _consumer.WaitForConsumerHostBootstrapToCompleteAsync().ConfigureAwait(false);
        await OpenConfirmChannelsAsync().ConfigureAwait(false);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _producer.Dispose();
        _consumer.Dispose();
        await TestBase.RabbitApi.DeleteExchangeAsync(LateExchange).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task PublishToMissingExchange_ValidMessagesPublishedBehindIt_AreConfirmed()
    {
        // Arrange
        var before = await PublishEventsAsync(ExistingExchange, 3).ConfigureAwait(false);

        // Act
        Task bad;
        Task[] behind;
        using (_spy.HoldMainChannelRecovery())
        {
            bad = await PublishEventAsync(MissingExchange).ConfigureAwait(false);
            behind = await PublishEventsAsync(ExistingExchange, 3).ConfigureAwait(false);
        }

        // Assert
        await Task.WhenAll(before.Concat(behind)).ConfigureAwait(false);
        await Assert.ThrowsAsync<RetryLimitExceededException>(() => bad).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task PublishToInternalExchange_ValidMessagesPublishedBehindIt_AreConfirmed()
    {
        // Arrange
        var internalExchange = $"test.publish.refusal.internal.{Guid.NewGuid():N}";
        await TestBase.RabbitApi.DeclareExchangeAsync(internalExchange, "direct", isInternal: true).ConfigureAwait(false);

        try
        {
            // Act
            Task bad;
            Task[] behind;
            using (_spy.HoldMainChannelRecovery())
            {
                bad = await PublishEventAsync(internalExchange).ConfigureAwait(false);
                behind = await PublishEventsAsync(ExistingExchange, 3).ConfigureAwait(false);
            }

            // Assert
            await Task.WhenAll(behind).ConfigureAwait(false);
            await Assert.ThrowsAsync<RetryLimitExceededException>(() => bad).ConfigureAwait(false);
        }
        finally
        {
            await TestBase.RabbitApi.DeleteExchangeAsync(internalExchange).ConfigureAwait(false);
        }
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task InterleavedPublishesToMissingExchange_ValidMessagesAreConfirmed()
    {
        // Act
        Task bad1, bad2;
        Task[] good1, good2;
        using (_spy.HoldMainChannelRecovery())
        {
            bad1 = await PublishEventAsync(MissingExchange).ConfigureAwait(false);
            good1 = await PublishEventsAsync(ExistingExchange, 2).ConfigureAwait(false);
            bad2 = await PublishEventAsync(MissingExchange).ConfigureAwait(false);
            good2 = await PublishEventsAsync(ExistingExchange, 2).ConfigureAwait(false);
        }

        // Assert
        await Task.WhenAll(good1.Concat(good2)).ConfigureAwait(false);
        await Assert.ThrowsAsync<RetryLimitExceededException>(() => bad1).ConfigureAwait(false);
        await Assert.ThrowsAsync<RetryLimitExceededException>(() => bad2).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task PublishToMissingExchange_ExchangeDeclaredWhileRetrying_IsConfirmed()
    {
        // Arrange
        var late = await PublishEventAsync(LateExchange).ConfigureAwait(false);
        await _producerLog.WaitForRefusalsAsync(1).ConfigureAwait(false);

        // Act
        await TestBase.RabbitApi.DeclareExchangeAsync(LateExchange, "fanout").ConfigureAwait(false);

        // Assert
        await late.ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task QueryToMissingExchange_QueryPublishedBehindIt_GetsItsReply()
    {
        // Act
        Task bad;
        Task<CarrotResponse<RefusalTestQuery, RefusalTestQuery.Response>> behind;
        using (_spy.HoldMainChannelRecovery())
        {
            bad = await QueryAsync(new MissingExchangeQuery()).ConfigureAwait(false);
            behind = await QueryAsync(new RefusalTestQuery { Id = 2 }).ConfigureAwait(false);
        }

        // Assert
        Assert.AreEqual(2, (await behind.ConfigureAwait(false)).Content?.Id);
        await Assert.ThrowsAsync<RetryLimitExceededException>(() => bad).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task TwoQueriesToMissingExchange_QueryBetweenThem_GetsItsReplyBeforeTheSecondIsRetried()
    {
        // Arrange
        Task bad1, bad2;
        Task<CarrotResponse<RefusalTestQuery, RefusalTestQuery.Response>> between;
        using (_spy.HoldMainChannelRecovery())
        {
            bad1 = await QueryAsync(new MissingExchangeQuery()).ConfigureAwait(false);
            between = await QueryAsync(new RefusalTestQuery { Id = 2, WaitForGate = true }).ConfigureAwait(false);
            bad2 = await QueryAsync(new MissingExchangeQuery()).ConfigureAwait(false);
        }

        await RefusalTestQueryHandler.GateReached.ConfigureAwait(false);
        var refusalsWhileTheHandlerRuns = _producerLog.Refusals;

        // Act
        RefusalTestQueryHandler.OpenGate();

        // Assert
        Assert.AreEqual(2, (await between.ConfigureAwait(false)).Content?.Id);
        Assert.AreEqual(1, refusalsWhileTheHandlerRuns, "the second refused query was published while the reply was pending");
        await Assert.ThrowsAsync<RetryLimitExceededException>(() => bad1).ConfigureAwait(false);
        await Assert.ThrowsAsync<RetryLimitExceededException>(() => bad2).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task QueryToMissingExchange_UnansweredQueryBehindIt_NextQueryGetsItsReplyAfterTheTtl()
    {
        // Act
        Task bad, unanswered;
        Task<CarrotResponse<RefusalTestQuery, RefusalTestQuery.Response>> behind;
        using (_spy.HoldMainChannelRecovery())
        {
            bad = await QueryAsync(new MissingExchangeQuery()).ConfigureAwait(false);
            unanswered = await QueryAsync(new UnansweredQuery(), ttl: 2_000).ConfigureAwait(false);
            behind = await QueryAsync(new RefusalTestQuery { Id = 3 }).ConfigureAwait(false);
        }

        // Assert
        Assert.AreEqual(3, (await behind.ConfigureAwait(false)).Content?.Id);
        await Assert.ThrowsAsync<OperationCanceledException>(() => unanswered).ConfigureAwait(false);
        await Assert.ThrowsAsync<RetryLimitExceededException>(() => bad).ConfigureAwait(false);
    }

    [TestMethod]
    [Timeout(30_000)]
    public async Task QuarantinedQuery_ExchangeDeclaredWhileAnotherRefusedQueryRetries_GetsItsReply()
    {
        // Arrange
        Task bad;
        Task<CarrotResponse<LateExchangeQuery, RefusalTestQuery.Response>> late;
        using (_spy.HoldMainChannelRecovery())
        {
            bad = await QueryAsync(new MissingExchangeQuery()).ConfigureAwait(false);
            late = await QueryAsync(new LateExchangeQuery { Id = 4 }).ConfigureAwait(false);
        }

        await _producerLog.WaitForRefusalsAsync(2).ConfigureAwait(false);

        // Act
        await DeclareLateExchangeRoutedToTheHandlersAsync().ConfigureAwait(false);

        // Assert
        Assert.AreEqual(4, (await late.ConfigureAwait(false)).Content?.Id);
        await Assert.ThrowsAsync<RetryLimitExceededException>(() => bad).ConfigureAwait(false);
    }

    // Documents at-least-once: the broker routed the query, but the close cut off its confirm, so it is published again.
    [TestMethod]
    [Timeout(30_000)]
    public async Task QueryRoutedBeforeARefusedPublish_ConfirmCutOffByTheClose_HandlerRunsTwice()
    {
        // Arrange
        _spy.DropConfirmsOnOpenMainChannels();

        // Act
        var routed = await QueryAsync(new RefusalTestQuery { Id = 5 }).ConfigureAwait(false);
        var bad = await QueryAsync(new MissingExchangeQuery()).ConfigureAwait(false);
        var response = await routed.ConfigureAwait(false);
        await Wait.UntilAsync(() => RefusalTestQueryHandler.Runs(5) >= 2, "second handler run").ConfigureAwait(false);
        await Assert.ThrowsAsync<RetryLimitExceededException>(() => bad).ConfigureAwait(false);

        // Assert
        Assert.AreEqual(5, response.Content?.Id);
        Assert.AreEqual(2, RefusalTestQueryHandler.Runs(5));
    }

    private CarrotHelper CreateConsumer()
    {
        return new CarrotHelper(
            "PublishRefusalTest.Consumer",
            builder =>
            {
                var exchange = builder.Exchanges.AddDirect<RefusalTestExchange>();
                var queue = builder.Queues.AddQuorum(QueueName).WithConsumer(c => c.WithSingleAck());
                builder.Handlers.AddQuery<RefusalTestQueryHandler, RefusalTestQuery, RefusalTestQuery.Response>().BindTo(exchange, queue);
                builder.Handlers.AddQuery<LateExchangeQueryHandler, LateExchangeQuery, RefusalTestQuery.Response>().BindTo(exchange, queue);
                builder.StartAsHostedService();
            });
    }

    private CarrotHelper CreateProducer()
    {
        const string producerName = "PublishRefusalTest.Producer";

        return new CarrotHelper(
            producerName,
            builder => builder.ConfigureBrokerConnection(
                configureOptions: options =>
                {
                    TestBase.ConfigureBroker(options);
                    options.ServiceName = producerName;
                    options.PublisherConfirm.RetryLimit = RetryLimit;
                }),
            services =>
            {
                services.AddSingleton<ILoggerProvider>(_producerLog);
                services.AddSingleton<BrokerConnection>();
                services.Replace(
                    ServiceDescriptor.Singleton<IBrokerConnection>(sp => _spy.Decorate(
                        sp.GetRequiredService<BrokerConnection>(),
                        sp.GetRequiredService<IOptions<BrokerConnectionOptions>>().Value,
                        sp.GetRequiredService<IProtocolSerializer>(),
                        sp.GetRequiredService<ILoggerFactory>())));
            });
    }

    private async Task OpenConfirmChannelsAsync()
    {
        await (await PublishEventAsync(ExistingExchange).ConfigureAwait(false)).ConfigureAwait(false);
        await (await QueryAsync(new RefusalTestQuery { Id = 1 }).ConfigureAwait(false)).ConfigureAwait(false);
    }

    private Task<Task> PublishEventAsync(string exchange)
    {
        var header = new CarrotHeader { Exchange = exchange, RoutingKey = "test.publish.refusal", MessageProperties = MessageProperties.Default };

        return _spy.PublishInOrderAsync(() => _transport.SendAsync(new CarrotMessage(header, string.Empty), CancellationToken.None));
    }

    private async Task<Task[]> PublishEventsAsync(string exchange, int count)
    {
        var published = new Task[count];
        for (var i = 0; i < count; i++) published[i] = await PublishEventAsync(exchange).ConfigureAwait(false);

        return published;
    }

    private Task<Task<CarrotResponse<TQuery, RefusalTestQuery.Response>>> QueryAsync<TQuery, TEndPoint>(
        IQuery<TQuery, RefusalTestQuery.Response, TEndPoint> query,
        int ttl = 20_000)
        where TQuery : IQuery<TQuery, RefusalTestQuery.Response, TEndPoint>
        where TEndPoint : EndPointBase, new()
    {
        var properties = MessageProperties.Default with { Ttl = ttl };

        return _spy.PublishInOrderAsync(() => _client.SendReceiveAsync(query, messageProperties: properties));
    }

    // A fanout exchange bound to RefusalTestExchange, which routes the query to its handler.
    private static async Task DeclareLateExchangeRoutedToTheHandlersAsync()
    {
        await TestBase.RabbitApi.DeclareExchangeAsync(LateExchange, "fanout").ConfigureAwait(false);
        await TestBase.RabbitApi.BindExchangeAsync(LateExchange, RefusalTestExchange.Name, string.Empty).ConfigureAwait(false);
    }

    public class RefusalTestExchange : ExchangeEndPoint
    {
        public const string Name = "test.publish.refusal.exchange";

        public RefusalTestExchange() : base(Name)
        {
        }
    }

    public class MissingExchangeEndPoint : ExchangeEndPoint
    {
        public MissingExchangeEndPoint() : base(MissingExchange)
        {
        }
    }

    public class LateExchangeEndPoint : ExchangeEndPoint
    {
        public LateExchangeEndPoint() : base(LateExchange)
        {
        }
    }

    // Routed nowhere: the broker confirms it and no reply ever comes.
    public class UnroutedEndPoint : ExchangeEndPoint
    {
        public UnroutedEndPoint() : base(ExistingExchange)
        {
        }
    }

    public class RefusalTestQuery : IQuery<RefusalTestQuery, RefusalTestQuery.Response, RefusalTestExchange>
    {
        public int Id { get; set; }

        public bool WaitForGate { get; set; }

        public class Response
        {
            public int Id { get; set; }
        }
    }

    public class MissingExchangeQuery : IQuery<MissingExchangeQuery, RefusalTestQuery.Response, MissingExchangeEndPoint>;

    public class UnansweredQuery : IQuery<UnansweredQuery, RefusalTestQuery.Response, UnroutedEndPoint>;

    public class LateExchangeQuery : IQuery<LateExchangeQuery, RefusalTestQuery.Response, LateExchangeEndPoint>
    {
        public int Id { get; set; }
    }

    /// <summary>
    /// Answers with the query's id, counts its runs per id, and holds queries with <c>WaitForGate</c> until the gate opens.
    /// </summary>
    public sealed class RefusalTestQueryHandler : QueryHandlerBase<RefusalTestQuery, RefusalTestQuery.Response>
    {
        private static readonly ConcurrentDictionary<int, int> s_runs = new();
        private static TaskCompletionSource<bool> s_gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private static TaskCompletionSource<bool> s_gateReached = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static Task GateReached => s_gateReached.Task;

        public static int Runs(int id)
        {
            return s_runs.TryGetValue(id, out var runs) ? runs : 0;
        }

        public static void OpenGate()
        {
            s_gate.TrySetResult(true);
        }

        public static void Reset()
        {
            s_runs.Clear();
            s_gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            s_gateReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public override async Task<IHandlerResult> HandleAsync(
            RefusalTestQuery query,
            ConsumerContext consumerContext,
            CancellationToken cancellationToken)
        {
            s_runs.AddOrUpdate(query.Id, 1, (_, runs) => runs + 1);
            if (query.WaitForGate)
            {
                s_gateReached.TrySetResult(true);
                await s_gate.Task.ConfigureAwait(false);
            }

            return Ok(new RefusalTestQuery.Response { Id = query.Id });
        }
    }

    public sealed class LateExchangeQueryHandler : QueryHandlerBase<LateExchangeQuery, RefusalTestQuery.Response>
    {
        public override Task<IHandlerResult> HandleAsync(
            LateExchangeQuery query,
            ConsumerContext consumerContext,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Ok(new RefusalTestQuery.Response { Id = query.Id }));
        }
    }
}