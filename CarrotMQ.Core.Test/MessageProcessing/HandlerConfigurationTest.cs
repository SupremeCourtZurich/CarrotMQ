using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.Dto;
using CarrotMQ.Core.Handlers;
using CarrotMQ.Core.Handlers.HandlerResults;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.Test.Helper;
using Microsoft.Extensions.DependencyInjection;

namespace CarrotMQ.Core.Test.MessageProcessing;

[TestClass]
public class HandlerConfigurationTest
{
    private HandlerCollection _handlerCollection = null!;

    [TestInitialize]
    public void Setup()
    {
        _handlerCollection = new HandlerCollection(new ServiceCollection(), new BindingCollection());
    }

    [TestMethod]
    public void HandlerConfiguration_AddCommandHandler()
    {
        string expectedHandlerKey = $"{typeof(HandlerConfigurationTest).FullName}+{nameof(TestCommand)}";
        _handlerCollection.AddCommand<TestCommandHandler, TestCommand, TestResponse>();

        Assert.AreEqual(1, _handlerCollection.GetHandlers().Count);
        var handlerProcessor = _handlerCollection.GetHandlers()[expectedHandlerKey];
        Assert.AreEqual(expectedHandlerKey, handlerProcessor.HandlerKey);
        Assert.AreEqual(typeof(TestCommandHandler), handlerProcessor.HandlerType);
    }

    [TestMethod]
    public void HandlerConfiguration_AddQueryHandler()
    {
        string expectedHandlerKey = $"{typeof(HandlerConfigurationTest).FullName}+{nameof(TestQuery)}";
        _handlerCollection.AddQuery<TestQueryHandler, TestQuery, TestResponse>();

        Assert.AreEqual(1, _handlerCollection.GetHandlers().Count);
        var handlerProcessor = _handlerCollection.GetHandlers()[expectedHandlerKey];
        Assert.AreEqual(expectedHandlerKey, handlerProcessor.HandlerKey);
        Assert.AreEqual(typeof(TestQueryHandler), handlerProcessor.HandlerType);
    }

    [TestMethod]
    public void HandlerConfiguration_AddEventHandler()
    {
        string expectedHandlerKey = $"{typeof(HandlerConfigurationTest).FullName}+{nameof(TestEvent)}";
        _handlerCollection.AddEvent<TestEventHandler, TestEvent>();

        Assert.AreEqual(1, _handlerCollection.GetHandlers().Count);
        var handlerProcessor = _handlerCollection.GetHandlers()[expectedHandlerKey];
        Assert.AreEqual(expectedHandlerKey, handlerProcessor.HandlerKey);
        Assert.AreEqual(typeof(TestEventHandler), handlerProcessor.HandlerType);
    }

    [TestMethod]
    public void HandlerConfiguration_AddCustomEventHandler()
    {
        string expectedHandlerKey = $"{typeof(HandlerConfigurationTest).FullName}+{nameof(TestCustomEvent)}";
        _handlerCollection.AddCustomRoutingEvent<TestCustomEventHandler, TestCustomEvent>();

        Assert.AreEqual(1, _handlerCollection.GetHandlers().Count);
        var handlerProcessor = _handlerCollection.GetHandlers()[expectedHandlerKey];
        Assert.AreEqual(expectedHandlerKey, handlerProcessor.HandlerKey);
        Assert.AreEqual(typeof(TestCustomEventHandler), handlerProcessor.HandlerType);
    }

    [TestMethod]
    public void HandlerConfiguration_AddResponseHandler()
    {
        string expectedHandlerKey = $"Response:{typeof(TestCommand).FullName}";

        _handlerCollection.AddResponse<TestResponseHandlerBase, TestCommand, TestResponse>();

        Assert.AreEqual(1, _handlerCollection.GetHandlers().Count);
        var handlerProcessor = _handlerCollection.GetHandlers()[expectedHandlerKey];
        Assert.AreEqual(expectedHandlerKey, handlerProcessor.HandlerKey);
        Assert.AreEqual(typeof(TestResponseHandlerBase), handlerProcessor.HandlerType);
    }

    [TestMethod]
    [ExpectedException(typeof(DuplicateHandlerKeyException))]
    public void HandlerConfiguration_AddCommandHandler_DifferentHandlers_With_Same_RequestType_ThrowsException()
    {
        _handlerCollection.AddCommand<TestCommandHandler, TestCommand, TestResponse>();
        _handlerCollection.AddCommand<TestCommandHandlerBase2, TestCommand, TestResponse>();
    }

    [TestMethod]
    [ExpectedException(typeof(GenericMessageTypeException))]
    public void HandlerConfiguration_AddCommandHandler_GenericRequestType_ThrowsException()
    {
        _handlerCollection.AddCommand<TestCommandGenericHandler, TestCommandGeneric<string>, TestResponse>();
    }

    #region TestClasses

    public class TestCommand : ICommand<TestCommand, TestResponse, TestQueueEndPoint>;

    public class TestQuery : IQuery<TestQuery, TestResponse, TestQueueEndPoint>;

    public class TestEvent : IEvent<TestEvent, TestExchangeEndPoint>;

    public class TestCustomEvent : ICustomRoutingEvent<TestCustomEvent>
    {
        public string Exchange { get; set; } = string.Empty;

        public string RoutingKey { get; set; } = string.Empty;
    }

    public class TestResponse;

    public class TestCommandGeneric<TMyType> : ICommand<TestCommandGeneric<TMyType>, TestResponse, TestQueueEndPoint>;

    public class TestCommandGenericHandler : CommandHandlerBase<TestCommandGeneric<string>, TestResponse>
    {
        public override Task<IHandlerResult> HandleAsync(
            TestCommandGeneric<string> command,
            ConsumerContext consumerContext,
            CancellationToken cancellationToken)
        {
            return Ok(new TestResponse()).AsTask();
        }
    }

    public class TestCommandHandler : CommandHandlerBase<TestCommand, TestResponse>
    {
        public override Task<IHandlerResult> HandleAsync(
            TestCommand message,
            ConsumerContext consumerContext,
            CancellationToken cancellationToken)
        {
            return Ok(new TestResponse()).AsTask();
        }
    }

    public class TestEventHandler : EventHandlerBase<TestEvent>
    {
        public override Task<IHandlerResult> HandleAsync(
            TestEvent message,
            ConsumerContext consumerContext,
            CancellationToken cancellationToken)
        {
            return Ok().AsTask();
        }
    }

    public class TestCustomEventHandler : EventHandlerBase<TestCustomEvent>
    {
        public override Task<IHandlerResult> HandleAsync(
            TestCustomEvent message,
            ConsumerContext consumerContext,
            CancellationToken cancellationToken)
        {
            return Ok().AsTask();
        }
    }

    public class TestQueryHandler : QueryHandlerBase<TestQuery, TestResponse>
    {
        public override Task<IHandlerResult> HandleAsync(
            TestQuery message,
            ConsumerContext consumerContext,
            CancellationToken cancellationToken)
        {
            return Ok(new TestResponse()).AsTask();
        }
    }

    public class TestResponseHandlerBase : ResponseHandlerBase<TestCommand, TestResponse>
    {
        public override Task<IHandlerResult> HandleAsync(
            CarrotResponse<TestCommand, TestResponse> carrotResponse,
            ConsumerContext consumerContext,
            CancellationToken cancellationToken)
        {
            return Ok().AsTask();
        }
    }

    public class TestCommandHandlerBase2 : CommandHandlerBase<TestCommand, TestResponse>
    {
        public override Task<IHandlerResult> HandleAsync(TestCommand command, ConsumerContext consumerContext, CancellationToken cancellationToken)
        {
            return Ok(new TestResponse()).AsTask();
        }
    }

    #endregion
}