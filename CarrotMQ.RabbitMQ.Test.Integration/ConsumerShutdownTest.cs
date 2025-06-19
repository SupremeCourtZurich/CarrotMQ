using CarrotMQ.Core;
using CarrotMQ.Core.Dto;
using CarrotMQ.Core.EndPoints;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CarrotMQ.RabbitMQ.Test.Integration;

[TestClass]
public class ConsumerShutdownTest
{
    /// <summary>
    /// Checks that the consumer stops receiving messages even if messages are still beeing processed (disconnect consumer, but
    /// keep consumer channel open for ack)
    /// </summary>
    [TestMethod]
    public async Task ConsumerClosesInBound_While_Graceful_Shutdown()
    {
        // Start host1
        var host1 = StartHost1();
        await host1.WaitForConsumerHostBootstrapToCompleteAsync();

        // Send message 1 (this message should reach ConsumerShutdownQueryHandler1 on host1)
        var sendTask1 = TestBase.CarrotClient.SendReceiveAsync(
            new ConsumerShutdownQuery { Data = $"{nameof(ConsumerShutdownTest)}_1" },
            messageProperties: new MessageProperties { Ttl = 10000 });

        var semaphoreInjection = host1.Host.Services.GetRequiredService<ConsumerShutdownQueryHandler1.SemaphoreInjector>();

        // wait for it to reach the ConsumerShutdownQueryHandler1
        await semaphoreInjection.HandlerHit.WaitAsync(TimeSpan.FromSeconds(10));

        // Dispose host1 --> Gracefull shutdown of the consumer
        host1.Dispose();
        // Wait for the Handler to receice the shutdown via its cancellationToken
        await semaphoreInjection.HandlerShutdownReceived.WaitAsync(TimeSpan.FromSeconds(3));

        // Now the 1st message is "blocked" inside the ConsumerShutdownQueryHandler1

        // Start host2
        using var host2 = StartHost2();
        await host2.WaitForConsumerHostBootstrapToCompleteAsync();

        // Send 3 new messages (they should reach the ConsumerShutdownQueryHandler2 on host2)
        // even though host1 is still running and processing message 1
        await Task.Delay(500); // Delay to make sure the consumer on host1 has been canceled on the broker
        var r2 = await TestBase.CarrotClient.SendReceiveAsync(
            new ConsumerShutdownQuery { Data = $"{nameof(ConsumerShutdownTest)}_2" },
            messageProperties: new MessageProperties { Ttl = 10000 });
        var r3 = await TestBase.CarrotClient.SendReceiveAsync(
            new ConsumerShutdownQuery { Data = $"{nameof(ConsumerShutdownTest)}_3" },
            messageProperties: new MessageProperties { Ttl = 10000 });
        var r4 = await TestBase.CarrotClient.SendReceiveAsync(
            new ConsumerShutdownQuery { Data = $"{nameof(ConsumerShutdownTest)}_4" },
            messageProperties: new MessageProperties { Ttl = 10000 });

        // Allow message 1 to finish 
        semaphoreInjection.AllowHandlerToComplete.Release();

        // wait for the response of message 1
        var r1 = await sendTask1;

        // Check that the 1st message has reached the ConsumerShutdownQueryHandler1 of host1
        Assert.AreEqual(1, r1.Content?.ResponseFrom);

        // Check that the messages 2,3 and 4 have reached the ConsumerShutdownQueryHandler2 of host2
        Assert.AreEqual(2, r2.Content?.ResponseFrom);
        Assert.AreEqual(2, r3.Content?.ResponseFrom);
        Assert.AreEqual(2, r4.Content?.ResponseFrom);
    }

    private static CarrotHelper StartHost2()
    {
        return new CarrotHelper(
            "Consumer2",
            builder =>
            {
                builder.Queues.AddQuorum<ConsumerShutdownQueue>().WithConsumer(c => c.WithSingleAck());

                builder.Handlers.AddQuery<ConsumerShutdownQueryHandler2, ConsumerShutdownQuery, ConsumerShutdownQuery.Response>();
                builder.StartAsHostedService();
            });
    }

    private static CarrotHelper StartHost1()
    {
        return new CarrotHelper(
            "Consumer1",
            builder =>
            {
                builder.Queues.AddQuorum<ConsumerShutdownQueue>().WithConsumer(c => c.WithSingleAck());

                builder.Handlers.AddQuery<ConsumerShutdownQueryHandler1, ConsumerShutdownQuery, ConsumerShutdownQuery.Response>();

                builder.StartAsHostedService();
            },
            services => { services.AddSingleton<ConsumerShutdownQueryHandler1.SemaphoreInjector>(); });
    }

    public class ConsumerShutdownQueue : QueueEndPoint
    {
        public const string Name = "consumer.shutdown.queue";

        public ConsumerShutdownQueue() : base(Name)
        {
        }
    }

    public class ConsumerShutdownQuery : IQuery<ConsumerShutdownQuery, ConsumerShutdownQuery.Response, ConsumerShutdownQueue>
    {
        public string Data { get; set; } = "";

        public class Response
        {
            public Response()
            {
            }

            public Response(int responseFrom)
            {
                ResponseFrom = responseFrom;
            }

            public int ResponseFrom { get; set; }
        }
    }

    public class ConsumerShutdownQueryHandler1 : QueryHandlerBase<ConsumerShutdownQuery, ConsumerShutdownQuery.Response>
    {
        private readonly SemaphoreInjector _injector;
        private readonly ILogger<ConsumerShutdownQueryHandler1> _logger;

        public ConsumerShutdownQueryHandler1(SemaphoreInjector injector, ILogger<ConsumerShutdownQueryHandler1> logger)
        {
            _injector = injector;
            _logger = logger;
        }

        public override async Task<IHandlerResult> HandleAsync(
            ConsumerShutdownQuery message,
            ConsumerContext consumerContext,
            CancellationToken cancellationToken)
        {
            _injector.HandlerHit.Release();
            _logger.LogTrace("Handler1 HIT");
            // ReSharper disable once UseAwaitUsing
            using var registration = cancellationToken.Register(
                () =>
                {
                    _logger.LogTrace("Handler1 SHUTDOWN RECEIVED");

                    _injector.HandlerShutdownReceived.Release();
                });

            // ReSharper disable once MethodSupportsCancellation
            await _injector.AllowHandlerToComplete.WaitAsync(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            _logger.LogTrace("Handler1 FINISHING");

            return Ok(new ConsumerShutdownQuery.Response(1));
        }

        public class SemaphoreInjector
        {
            public SemaphoreSlim AllowHandlerToComplete = new(0);
            public SemaphoreSlim HandlerHit = new(0);
            public SemaphoreSlim HandlerShutdownReceived = new(0);
        }
    }

    public class ConsumerShutdownQueryHandler2 : QueryHandlerBase<ConsumerShutdownQuery, ConsumerShutdownQuery.Response>
    {
        public override Task<IHandlerResult> HandleAsync(
            ConsumerShutdownQuery message,
            ConsumerContext consumerContext,
            CancellationToken cancellationToken)
        {
            return Ok(new ConsumerShutdownQuery.Response(2)).AsTask();
        }
    }
}