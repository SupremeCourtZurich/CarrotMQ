using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.Dto;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.MessageProcessing.Middleware;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;
using CarrotMQ.Core.Test.Helper;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

#pragma warning disable MA0147 // Avoid async void method for delegate (for NSubstite  Received.InOrder)

namespace CarrotMQ.Core.Test.MessageProcessing;

[TestClass]
[TestCategory("UnitTest")]
public class CarrotMiddlewareTest
{
    private readonly ICarrotSerializer _carrotSerializer = new DefaultCarrotSerializer();

    private IMessageDistributor _messageDistributor = null!;

    private List<ICarrotMiddleware> _middlewareCollection = [];

    private TestCommandHandler _testHandler = null!;

    [TestInitialize]
    public void Setup()
    {
        _middlewareCollection = [];
        _testHandler = new TestCommandHandler();

        var middlewareProcessor = new MiddlewareProcessor(_middlewareCollection);

        var dependencyInjector = Substitute.For<IDependencyInjector>();
        dependencyInjector.CreateAsyncScope().Returns(dependencyInjector);
        dependencyInjector.GetCarrotSerializer().Returns(_carrotSerializer);
        dependencyInjector.GetMiddlewareProcessor().Returns(middlewareProcessor);

        var handlerCollection = new HandlerCollection(new ServiceCollection(), new BindingCollection());
        _messageDistributor = new MessageDistributor(
            dependencyInjector,
            handlerCollection,
            Substitute.For<IResponseSender>(),
            TestLoggerFactory.CreateLogger<MessageDistributor>());

        handlerCollection.AddCommand<TestCommandHandler, TestCommand, TestResponse>();
        dependencyInjector.CreateHandler<TestCommandHandler, TestCommand, TestResponse>().Returns(_testHandler);
    }

    [TestMethod]
    public async Task InvokeAsyncIsCalled_InServiceDistributor()
    {
        //Arrange
        var runId = Guid.NewGuid();

        var middleware = Substitute.For<ICarrotMiddleware>();
        _middlewareCollection.Add(middleware);

        var message = CreateTestMessage(runId);

        //Act
        await _messageDistributor.DistributeAsync(message, default);

        //Assert
        await middleware.Received(1).InvokeAsync(Arg.Any<MiddlewareContext>(), Arg.Any<Func<Task>>());
    }

    [TestMethod]
    public async Task PostProcessCallsMiddlewareInOrder()
    {
        //Arrange

        var runId = Guid.NewGuid();

        var middleware1 = GetMiddlewareSubstitute();
        var middleware2 = GetMiddlewareSubstitute();
        var middleware3 = GetMiddlewareSubstitute();

        _middlewareCollection.Add(middleware1);
        _middlewareCollection.Add(middleware2);
        _middlewareCollection.Add(middleware3);

        var message = CreateTestMessage(runId);

        //Act
        await _messageDistributor.DistributeAsync(message, default);

        //Assert
        Received.InOrder(
            async void () =>
            {
                await middleware1.InvokeAsync(Arg.Any<MiddlewareContext>(), Arg.Any<Func<Task>>()).ConfigureAwait(false);
                await middleware2.InvokeAsync(Arg.Any<MiddlewareContext>(), Arg.Any<Func<Task>>()).ConfigureAwait(false);
                await middleware3.InvokeAsync(Arg.Any<MiddlewareContext>(), Arg.Any<Func<Task>>()).ConfigureAwait(false);
            });
    }

    private static ICarrotMiddleware GetMiddlewareSubstitute()
    {
        var middleware = Substitute.For<ICarrotMiddleware>();
        middleware.InvokeAsync(Arg.Any<MiddlewareContext>(), Arg.Any<Func<Task>>())
            .Returns(async c => await ((Func<Task>)c[1])().ConfigureAwait(false));

        return middleware;
    }

    private CarrotMessage CreateTestMessage(Guid runId)
    {
        var cmd = new TestCommand { RequestRunId = runId };
        var requestCarrotHeader = new CarrotHeader
        {
            CalledMethod = typeof(TestCommand).FullName!,
            ReplyRoutingKey = "MyRoutingKey"
        };

        return new CarrotMessage(requestCarrotHeader, _carrotSerializer.Serialize(cmd));
    }

    private class TestCommand : ICommand<TestCommand, TestResponse, TestQueueEndPoint>
    {
        public Guid RequestRunId { get; set; }
    }

    private class TestResponse
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public Guid ResponseRunId { get; set; }
    }

    private sealed class TestCommandHandler : CommandHandlerBase<TestCommand, TestResponse>
    {
        public override Task<IHandlerResult> HandleAsync(TestCommand command, ConsumerContext consumerContext, CancellationToken cancellationToken)
        {
            return Ok(new TestResponse { ResponseRunId = command.RequestRunId }).AsTask();
        }
    }
}