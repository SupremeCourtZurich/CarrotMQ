using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.Dto;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;
using CarrotMQ.Core.Test.Helper;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace CarrotMQ.Core.Test;

[TestClass]
[TestCategory("UnitTest")]
public class MessageEnricherTest
{
    private readonly ICarrotSerializer _serializer = new DefaultCarrotSerializer();
    private ITransport _transport = null!;

    [TestInitialize]
    public void Setup()
    {
        var okResponse = _serializer.Serialize(new CarrotResponse { StatusCode = CarrotStatusCode.Ok });

        _transport = Substitute.For<ITransport>();
        _transport.SendReceiveAsync(Arg.Any<CarrotMessage>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(_ => Task.FromResult(new CarrotMessage { Payload = okResponse }));
    }

    [TestMethod]
    public async Task SendReceiveAsync_WithSyncEnricher_EnricherIsExecutedOnce()
    {
        //Arrange
        var enricherInvoked = 0;

        var client = CreateCarrotClient(new TestEnricher((_, _, _) => { enricherInvoked++; }));

        //Act
        await client.SendReceiveAsync(new TestCommand());

        //Assert
        Assert.AreEqual(1, enricherInvoked);
    }

    [TestMethod]
    public async Task SendAsync_WithSyncEnricher_EnricherIsExecutedOnce()
    {
        //Arrange
        var enricherInvoked = 0;

        var client = CreateCarrotClient(new TestEnricher((_, _, _) => { enricherInvoked++; }));

        //Act
        await client.SendAsync(new TestCommand());

        //Assert
        Assert.AreEqual(1, enricherInvoked);
    }

    [TestMethod]
    public async Task SendReceiveAsync_WithMultipleEnrichers_AllEnrichersExecuted()
    {
        //Arrange
        var firstInvoked = false;
        var secondInvoked = false;

        var client = CreateCarrotClient(
            new TestEnricher((_, _, _) => firstInvoked = true),
            new TestEnricher((_, _, _) => secondInvoked = true));

        //Act
        await client.SendReceiveAsync(new TestCommand());

        //Assert
        Assert.IsTrue(firstInvoked, $"{nameof(firstInvoked)} should be true");
        Assert.IsTrue(secondInvoked, $"{nameof(secondInvoked)} should be true");
    }

    [TestMethod]
    public async Task SendAsync_WithMultipleEnrichers_AllEnrichersExecuted()
    {
        //Arrange
        var firstInvoked = false;
        var secondInvoked = false;

        var client = CreateCarrotClient(
            new TestEnricher((_, _, _) => firstInvoked = true),
            new TestEnricher((_, _, _) => secondInvoked = true));

        //Act
        await client.SendAsync(new TestCommand());

        //Assert
        Assert.IsTrue(firstInvoked, $"{nameof(firstInvoked)} should be true");
        Assert.IsTrue(secondInvoked, $"{nameof(secondInvoked)} should be true");
    }

    [TestMethod]
    public async Task SendReceiveAsync_WhenChangingMessage_ChangeIsFoundInCarrotMessage()
    {
        //Arrange
        var newId = Guid.NewGuid();

        var client = CreateCarrotClient(
            new TestEnricher(
                (message, _, _) =>
                {
                    if (message is TestCommand cmd)
                    {
                        cmd.Id = newId;
                    }
                }));

        //Act
        await client.SendReceiveAsync(new TestCommand());

        //Assert
        await _transport.Received(1)
            .SendReceiveAsync(
                Arg.Is<CarrotMessage>(m => m.Payload!.Contains(newId.ToString())),
                Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task SendAsync_WhenChangingMessage_ChangeIsFoundInCarrotMessage()
    {
        //Arrange
        var newId = Guid.NewGuid();

        var client = CreateCarrotClient(
            new TestEnricher(
                (message, _, _) =>
                {
                    if (message is TestCommand cmd)
                    {
                        cmd.Id = newId;
                    }
                }));

        //Act
        await client.SendAsync(new TestCommand());

        //Assert
        await _transport.Received(1)
            .SendAsync(
                Arg.Is<CarrotMessage>(m => m.Payload!.Contains(newId.ToString())),
                Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task SendReceiveAsync_WhenChangingContext_ChangeIsFoundInCarrotMessage()
    {
        //Arrange
        var (key, value) = ("CustomHeader", "CustomValue");

        var client = CreateCarrotClient(new TestEnricher((_, ctx, _) => { ctx.CustomHeader[key] = value; }));

        //Act
        await client.SendReceiveAsync(new TestCommand());

        //Assert
        await _transport.Received(1)
            .SendReceiveAsync(
                Arg.Is<CarrotMessage>(m => m.Header.CustomHeader![key].Equals(value)),
                Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task SendAsync_WhenChangingContext_ChangeIsFoundInCarrotMessage()
    {
        //Arrange
        var (key, value) = ("CustomHeader", "CustomValue");

        var client = CreateCarrotClient(new TestEnricher((_, ctx, _) => { ctx.CustomHeader[key] = value; }));

        //Act
        await client.SendAsync(new TestCommand());

        //Assert
        await _transport.Received(1)
            .SendAsync(
                Arg.Is<CarrotMessage>(m => m.Header.CustomHeader![key].Equals(value)),
                Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task WhenAddedUsingExtensionMethod_DelegateEnrichersAreRegistered()
    {
        //Arrange
        var delegateExecuted = false;
        var services = new ServiceCollection();

        services.AddMessageEnricher((_, _, _) => delegateExecuted = true);

        var provider = services.BuildServiceProvider();
        var enricher = provider.GetRequiredService<IMessageEnricher>();

        //Act
        await enricher.EnrichMessageAsync(new TestCommand(), new Context(), default).ConfigureAwait(false);

        //Assert
        Assert.IsTrue(delegateExecuted, $"{nameof(delegateExecuted)} should be true");
    }

    [TestMethod]
    public async Task WhenConstructedFromServiceCollection_WhenAddingWithExtensionMethod_DelegatesAreExecutedInRegistrationOrder()
    {
        //Arrange
        var firstCalled = false;
        var secondCalled = false;
        //Arrange

        var services = new ServiceCollection();
        services.AddMessageEnricher((_, _, _) => firstCalled = true);
        services.AddMessageEnricher(
            (_, _, _) =>
            {
                if (!firstCalled) throw new Exception("Second delegate called before first");
                secondCalled = true;
            });

        var client = CreateCarrotClient(services.BuildServiceProvider().GetServices<IMessageEnricher>());

        //Act
        await client.SendAsync(new TestCommand());

        //Assert
        Assert.IsTrue(firstCalled, $"{nameof(firstCalled)} should be true");
        Assert.IsTrue(secondCalled, $"{nameof(secondCalled)} should be true");
    }

    [TestMethod]
    public async Task WhenConstructedFromServiceCollection_WhenUsingStronglyTypedEnrichers_DelegatesAreExecutedInRegistrationOrder()
    {
        //Arrange
        var firstCalled = false;
        var secondCalled = false;
        //Arrange

        var services = new ServiceCollection();
        services.AddSingleton<IMessageEnricher>(new TestEnricher((_, _, _) => firstCalled = true));
        services.AddSingleton<IMessageEnricher>(
            new TestEnricher(
                (_, _, _) =>
                {
                    if (!firstCalled) throw new Exception("Second delegate called before first");
                    secondCalled = true;
                }));

        var client = CreateCarrotClient(services.BuildServiceProvider().GetServices<IMessageEnricher>());

        //Act
        await client.SendAsync(new TestCommand());

        //Assert
        Assert.IsTrue(firstCalled, $"{nameof(firstCalled)} should be true");
        Assert.IsTrue(secondCalled, $"{nameof(secondCalled)} should be true");
    }

    [TestMethod]
    public async Task WhenConstructedFromServiceCollection_WhenUsingStronglyTypedEnrichersAndExtension_DelegatesAreExecutedInRegistrationOrder()
    {
        //Arrange
        var firstCalled = false;
        var secondCalled = false;
        //Arrange

        var services = new ServiceCollection();
        services.AddMessageEnricher((_, _, _) => firstCalled = true);
        services.AddSingleton<IMessageEnricher>(
            new TestEnricher(
                (_, _, _) =>
                {
                    if (!firstCalled) throw new Exception("Second delegate called before first");
                    secondCalled = true;
                }));

        var client = CreateCarrotClient(services.BuildServiceProvider().GetServices<IMessageEnricher>());

        //Act
        await client.SendAsync(new TestCommand());

        //Assert
        Assert.IsTrue(firstCalled, $"{nameof(firstCalled)} should be true");
        Assert.IsTrue(secondCalled, $"{nameof(secondCalled)} should be true");
    }

    private ICarrotClient CreateCarrotClient(IEnumerable<IMessageEnricher> messageEnrichers) => CreateCarrotClient(messageEnrichers.ToArray());

    private ICarrotClient CreateCarrotClient(params IMessageEnricher[] messageEnrichers)
    {
        return new CarrotClient(
            messageEnrichers,
            _transport,
            new DefaultRoutingKeyResolver(),
            _serializer);
    }

    private class TestCommand : ICommand<TestCommand, TestResponse, TestQueueEndPoint>
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public Guid Id { get; set; }
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private class TestResponse;
}

public class TestEnricher : IMessageEnricher
{
    private readonly Func<object, Context, CancellationToken, Task> _func;

    public TestEnricher(Func<object, Context, CancellationToken, Task> func)
    {
        _func = func;
    }

    public TestEnricher(Action<object, Context, CancellationToken> action) : this(
        (m, ctx, c) =>
        {
            action(m, ctx, c);

            return Task.CompletedTask;
        })
    {
    }

    public async Task EnrichMessageAsync(object message, Context context, CancellationToken token)
    {
        await Task.Run(() => _func(message, context, token), token).ConfigureAwait(false);
    }
}