using System.Threading.Channels;
using CarrotMQ.Core;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using CarrotMQ.RabbitMQ.Test.Integration.TestHelper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarrotMQ.RabbitMQ.Test.Integration;

[TestClass]
[TestCategory("Integration")]
public class ServiceLifetimeTest
{
    private const string QueueName = "test.service.lifetime.queue";
    private CancellationTokenSource _cts = null!;
    private IHost _host = null!;

    [TestInitialize]
    public void Initialize()
    {
        _cts = new CancellationTokenSource(30_000);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        // ReSharper disable once MethodHasAsyncOverload
        _cts.Cancel();
        _cts.Dispose();
        await _host.StopAsync();
    }

    private async Task<IHost> CreateHost(ServiceLifetime serviceLifetime)
    {
        var applicationBuilder = Host.CreateApplicationBuilder(["environment=Development"]);

        applicationBuilder.Logging.AddSimpleConsole(
            options =>
            {
                options.IncludeScopes = true;
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss.sss ";
            });

        applicationBuilder.Services.AddCarrotMqRabbitMq(
            builder =>
            {
                builder.ConfigureBrokerConnection(
                    configureOptions: options =>
                    {
                        TestBase.ConfigureBroker(options);
                        options.ServiceName = "DI-Registration";
                    });

                var exchange = builder.Exchanges.AddDirect<TestExchange>();

                var queue = builder.Queues.AddQuorum(QueueName)
                    .WithConsumer(
                        c => c
                            .WithPrefetchCount(0)
                            .WithSingleAck());

                builder.Handlers.AddEvent<DiTestEventHandler, TestEvent>()
                    .BindTo(exchange, queue);

                builder.StartAsHostedService();
            });

        applicationBuilder.Services.Add(new ServiceDescriptor(typeof(IHaveDependency), typeof(MyDependency), serviceLifetime));

        applicationBuilder.Services.AddSingleton(Channel.CreateBounded<DiTestEventHandler>(10));
        applicationBuilder.Services.AddSingleton(Channel.CreateBounded<IHaveDependency>(10));

        var host = applicationBuilder.Build();
        await host.StartAsync(_cts.Token).ConfigureAwait(false);

        return host;
    }

    [TestMethod]
    [DataRow(ServiceLifetime.Transient)]
    [DataRow(ServiceLifetime.Scoped)]
    [DataRow(ServiceLifetime.Singleton)]
    public async Task HandlerServiceLifetime(ServiceLifetime serviceLifetime)
    {
        _host = await CreateHost(serviceLifetime);
        var carrotClient = _host.Services.GetRequiredService<ICarrotClient>();

        await _host.WaitForConsumerHostBootstrapToCompleteAsync().ConfigureAwait(false);

        await carrotClient.PublishAsync(new TestEvent()).ConfigureAwait(false);

        var myDependencyInstances = _host.Services.GetRequiredService<Channel<IHaveDependency>>();
        var handlerInstances = _host.Services.GetRequiredService<Channel<DiTestEventHandler>>();

        var myDependency1 = await myDependencyInstances.Reader.ReadAsync(_cts.Token).ConfigureAwait(false);
        var handler1 = await handlerInstances.Reader.ReadAsync(_cts.Token).ConfigureAwait(false);

        await carrotClient.PublishAsync(new TestEvent()).ConfigureAwait(false);
        var myDependency2 = await myDependencyInstances.Reader.ReadAsync(_cts.Token).ConfigureAwait(false);
        var handler2 = await handlerInstances.Reader.ReadAsync(_cts.Token).ConfigureAwait(false);

        Assert.AreNotEqual(handler1, handler2);
        Assert.AreNotEqual(handler1.GetHashCode(), handler2.GetHashCode());
        if (serviceLifetime == ServiceLifetime.Singleton)
        {
            Assert.AreEqual(myDependency1, myDependency2);
            Assert.AreEqual(myDependency1.GetHashCode(), myDependency2.GetHashCode());
        }
        else
        {
            Assert.AreNotEqual(myDependency1, myDependency2);
            Assert.AreNotEqual(myDependency1.GetHashCode(), myDependency2.GetHashCode());
        }
    }

    // ReSharper disable once ClassNeverInstantiated.Global
    public class DiTestEventHandler : EventHandlerBase<TestEvent>
    {
        private readonly Channel<DiTestEventHandler> _handlerInstances;
        private readonly IHaveDependency _haveDependency;
        private readonly Channel<IHaveDependency> _myDependencyInstances;

        public DiTestEventHandler(
            IHaveDependency haveDependency,
            Channel<DiTestEventHandler> handlerInstances,
            Channel<IHaveDependency> myDependencyInstances)
        {
            _handlerInstances = handlerInstances;
            _myDependencyInstances = myDependencyInstances;
            _haveDependency = haveDependency;
        }

        public override async Task<IHandlerResult> HandleAsync(TestEvent @event, ConsumerContext consumerContext, CancellationToken cancellationToken)
        {
            await _myDependencyInstances.Writer.WriteAsync(_haveDependency, cancellationToken).ConfigureAwait(false);
            await _handlerInstances.Writer.WriteAsync(this, cancellationToken).ConfigureAwait(false);

            return Ok();
        }
    }

    public interface IHaveDependency;

    public class MyDependency : IHaveDependency;
}