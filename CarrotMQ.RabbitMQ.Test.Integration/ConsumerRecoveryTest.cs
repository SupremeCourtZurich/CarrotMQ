using CarrotMQ.Core;
using CarrotMQ.Core.Dto;
using CarrotMQ.Core.EndPoints;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;
using Microsoft.Extensions.DependencyInjection;

namespace CarrotMQ.RabbitMQ.Test.Integration;

[TestClass]
public class ConsumerRecoveryTest
{
    /// <summary>
    /// Checks that the consumer stops receiving messages even if messages are still beeing processed (disconnect consumer, but
    /// keep consumer channel open for ack)
    /// </summary>
    [TestMethod]
    public async Task Consumer_Recovered_After_QueueDeletion()
    {
        // Start host
        using var host1 = StartHost();
        await host1.WaitForConsumerHostBootstrapToCompleteAsync();

        // Send message (check that everything is initialized)
        await TestBase.CarrotClient.SendReceiveAsync(new ConsumerRecoveryQuery(), new Context(1000));

        // Delete queue on which we are consuming --> Consumer should be recovered automatically
        var connection = host1.Host.Services.GetRequiredService<IBrokerConnection>();
        var channel = await connection.GetPublisherChannelAsync();
        await channel.DeleteQueueAsync(ConsumerRecoveryQueue.Name);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Send RPCs until queue and consumer are recreated 
        CarrotResponse<ConsumerRecoveryQuery, ConsumerRecoveryQuery.Response>? r2 = null;
        do
        {
            try
            {
                r2 = await TestBase.CarrotClient.SendReceiveAsync(new ConsumerRecoveryQuery(), new Context(1000), cts.Token);
            }
            catch (Exception)
            {
                // ignore
            }
        } while (r2 == null && !cts.IsCancellationRequested);

        Assert.AreEqual(1, r2?.Content?.ResponseFrom);
    }

    private static CarrotHelper StartHost()
    {
        return new CarrotHelper(
            "Consumer",
            builder =>
            {
                builder.Queues.AddQuorum<ConsumerRecoveryQueue>().WithConsumer(c => c.WithSingleAck());

                builder.Handlers.AddQuery<ConsumerRecoveryQueryHandler, ConsumerRecoveryQuery, ConsumerRecoveryQuery.Response>();

                builder.StartAsHostedService();
            });
    }

    public class ConsumerRecoveryQueue : QueueEndPoint
    {
        public const string Name = "consumer.recovery.queue";

        public ConsumerRecoveryQueue() : base(Name)
        {
        }
    }

    public class ConsumerRecoveryQuery : IQuery<ConsumerRecoveryQuery, ConsumerRecoveryQuery.Response, ConsumerRecoveryQueue>
    {
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

    public class ConsumerRecoveryQueryHandler : QueryHandlerBase<ConsumerRecoveryQuery, ConsumerRecoveryQuery.Response>
    {
        public override Task<IHandlerResult> HandleAsync(
            ConsumerRecoveryQuery message,
            ConsumerContext consumerContext,
            CancellationToken cancellationToken)
        {
            return Ok(new ConsumerRecoveryQuery.Response(1)).AsTask();
        }
    }
}