using CarrotMQ.Core.Dto;
using CarrotMQ.Core.Dto.Internals;
using CarrotMQ.Core.EndPoints;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CarrotMQ.RabbitMQ.Test.Integration;

[TestClass]
public class DeclareTest
{
    private const string DeadLetterExchange = "declare-test.dead-letter.exchange";
    private const string DirectExchange = "declare-test-direct.exchange";
    private const string FanOutExchange = "declare-test-fanout.exchange";
    private const string TopicExchange = "declare-test-topic.exchange";
    private const string LocalRandomExchange = "declare-test-localrandomexchange.exchange";
    private const string Queue1Name = "declare-test-direct.quorum.queue";
    private const string Queue2Name = "declare-test-direct.classic.queue";
    private const string Queue3Name = "declare-test-direct.argument-test-1.queue";
    private const string Queue4Name = "declare-test-direct.argument-test-2.queue";

    [TestMethod]
    public async Task Declare_Exchange_Queues_Bindings_Test()
    {
        using var carrotHelper = new CarrotHelper(
            "Declare_Test",
            config =>
            {
                var deadLetterExchangeDefinition = config.Exchanges.AddFanOut(DeadLetterExchange);

                var directExchangeDefinition = config.Exchanges.AddDirect(DirectExchange);
                var fanOutExchangeDefinition = config.Exchanges.AddFanOut(FanOutExchange);
                var topicExchangeDefinition = config.Exchanges.AddTopic(TopicExchange);
                var localRandomExchangeDefinition = config.Exchanges.AddLocalRandom(LocalRandomExchange);

                var queue1 = config.Queues.AddQuorum<Queue1Ep>();
                queue1.WithConsumer(c => c.WithCustomArgument("TestArg", "TestValue"));
                queue1.WithConsumer();

                var queue2 = config.Queues.AddClassic(Queue2Name).WithConsumer().WithCustomArgument("x.delivery-limit", 3);
                config.Queues.AddClassic(Queue3Name)
                    .WithExclusivity()
                    .WithDurability(false)
                    .WithConsumer(c => c.WithSingleAck())
                    .WithDeadLetterExchange(deadLetterExchangeDefinition);
                config.Queues.AddQuorum(Queue4Name);
                config.Queues.UseQueue(Queue4Name).WithConsumer();

                localRandomExchangeDefinition.BindToQueue(queue2);
                fanOutExchangeDefinition.BindToQueue(queue1);
                topicExchangeDefinition.BindToQueue(queue1, "routingKey");

                directExchangeDefinition.BindToQueue(queue1, "routingKey");

                config.Handlers.AddEvent<GenericEventHandler<TestEvent>, TestEvent>()
                    .BindTo(directExchangeDefinition, queue1)
                    .BindTo(topicExchangeDefinition, queue2);

                config.StartAsHostedService();
            });

        await carrotHelper.Host.WaitForConsumerHostBootstrapToCompleteAsync().ConfigureAwait(false);
        var brokerConnectionConfig = carrotHelper.Host.Services.GetRequiredService<IOptions<BrokerConnectionOptions>>().Value;

        using var rabbitApi = new RabbitApi(brokerConnectionConfig, TestBase.RabbitMqContainerHttpPort);

        var exchanges = await rabbitApi.GetExchanges().ConfigureAwait(false);

        var declaredDirectExchange = exchanges?.Single(e => e.Name?.Equals(DirectExchange) ?? false);
        Assert.IsNotNull(declaredDirectExchange);
        Assert.AreEqual("direct", declaredDirectExchange.Type);

        var fanOutExchangeDeclared = exchanges?.Single(e => e.Name?.Equals(FanOutExchange) ?? false);
        Assert.IsNotNull(fanOutExchangeDeclared);
        Assert.AreEqual("fanout", fanOutExchangeDeclared.Type);

        var declaredTopicExchange = exchanges?.Single(e => e.Name?.Equals(TopicExchange) ?? false);
        Assert.IsNotNull(declaredTopicExchange);
        Assert.AreEqual("topic", declaredTopicExchange.Type);

        var declaredLocalRandomExchange = exchanges?.Single(e => e.Name?.Equals(LocalRandomExchange) ?? false);
        Assert.IsNotNull(declaredLocalRandomExchange);
        Assert.AreEqual("x-local-random", declaredLocalRandomExchange.Type);

        var queues = await rabbitApi.GetQueues().ConfigureAwait(false);

        var declaredClassicQueue = queues?.SingleOrDefault(q => q.Name?.Equals(Queue2Name) ?? false);
        Assert.IsNotNull(declaredClassicQueue);
        Assert.AreEqual("classic", declaredClassicQueue.Type);
        Assert.AreEqual(true, declaredClassicQueue.Durable);
        Assert.AreEqual(false, declaredClassicQueue.AutoDelete);
        Assert.AreEqual(false, declaredClassicQueue.Exclusive);

        var declaredQuorumQueue = queues?.SingleOrDefault(q => q.Name?.Equals(Queue1Name) ?? false);
        Assert.IsNotNull(declaredQuorumQueue);
        Assert.AreEqual("quorum", declaredQuorumQueue.Type);
        Assert.AreEqual(true, declaredQuorumQueue.Durable);
        Assert.AreEqual(false, declaredQuorumQueue.AutoDelete);
        Assert.AreEqual(false, declaredQuorumQueue.Exclusive);

        var declaredArgumentTestQueue1 = queues?.SingleOrDefault(q => q.Name?.Equals(Queue3Name) ?? false);
        Assert.IsNotNull(declaredArgumentTestQueue1);
        Assert.AreEqual("classic", declaredArgumentTestQueue1.Type);
        Assert.AreEqual(false, declaredArgumentTestQueue1.Durable);
        Assert.AreEqual(false, declaredArgumentTestQueue1.AutoDelete);
        Assert.AreEqual(true, declaredArgumentTestQueue1.Exclusive);

        var declaredArgumentTestQueue2 = queues?.SingleOrDefault(q => q.Name?.Equals(Queue4Name) ?? false);
        Assert.IsNotNull(declaredArgumentTestQueue2);
        Assert.AreEqual("quorum", declaredArgumentTestQueue2.Type);

        var directExchangeBindings = await rabbitApi.GetExchangeBindings(DirectExchange).ConfigureAwait(false);
        RabbitApi.Bindings? b1 = directExchangeBindings?.Single(
            b => "routingKey".Equals(b.RoutingKey) && DirectExchange.Equals(b.Source) && Queue1Name.Equals(b.Destination));
        Assert.IsNotNull(b1);
        RabbitApi.Bindings? b2 = directExchangeBindings?.Single(
            b => "CarrotMQ.RabbitMQ.Test.Integration.DeclareTest+TestEvent".Equals(b.RoutingKey)
                && DirectExchange.Equals(b.Source)
                && Queue1Name.Equals(b.Destination));
        Assert.IsNotNull(b2);

        var fanOutExchangeBindings = await rabbitApi.GetExchangeBindings(FanOutExchange).ConfigureAwait(false);
        RabbitApi.Bindings? b3 = fanOutExchangeBindings?.Single(
            b => string.IsNullOrEmpty(b.RoutingKey) && FanOutExchange.Equals(b.Source) && Queue1Name.Equals(b.Destination));
        Assert.IsNotNull(b3);

        var topicExchangeBindings = await rabbitApi.GetExchangeBindings(TopicExchange).ConfigureAwait(false);
        RabbitApi.Bindings? b4 = topicExchangeBindings?.Single(
            b => "routingKey".Equals(b.RoutingKey) && TopicExchange.Equals(b.Source) && Queue1Name.Equals(b.Destination));
        Assert.IsNotNull(b4);
        RabbitApi.Bindings? b5 = topicExchangeBindings?.Single(
            b => "CarrotMQ.RabbitMQ.Test.Integration.DeclareTest+TestEvent".Equals(b.RoutingKey)
                && TopicExchange.Equals(b.Source)
                && Queue2Name.Equals(b.Destination));
        Assert.IsNotNull(b5);

        var localRandomExchangeBindings = await rabbitApi.GetExchangeBindings(LocalRandomExchange).ConfigureAwait(false);
        RabbitApi.Bindings? b6 = localRandomExchangeBindings?.Single(
            b => string.IsNullOrEmpty(b.RoutingKey) && LocalRandomExchange.Equals(b.Source) && Queue2Name.Equals(b.Destination));
        Assert.IsNotNull(b6);
    }

    public class DirectExchangeEp : ExchangeEndPoint
    {
        public const string ExchangeName = "declare-test-direct.exchange";

        public DirectExchangeEp() : base(ExchangeName)
        {
        }
    }

    public class Queue1Ep : QueueEndPoint
    {
        public Queue1Ep() : base(Queue1Name)
        {
        }
    }

    public class TestEvent : IEvent<TestEvent, DirectExchangeEp>;

    public class GenericEventHandler<TEvent> : EventHandlerBase<TEvent> where TEvent : _IEvent<TEvent>
    {
        public override Task<IHandlerResult> HandleAsync(TEvent message, ConsumerContext consumerContext, CancellationToken cancellationToken)
        {
            return Ok().AsTask();
        }
    }
}