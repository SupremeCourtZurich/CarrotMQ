using CarrotMQ.Core.Dto;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.MessageSending;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;
using CarrotMQ.Core.Test.Helper;
using NSubstitute;

namespace CarrotMQ.Core.Test;

[TestClass]
public class CarrotClientResponseTests
{
    private ICarrotClient _carrotClient = null!;
    private ITransport _transport = null!;

    [TestInitialize]
    public void Initialize()
    {
        _transport = Substitute.For<ITransport>();
        ICarrotSerializer serializer = new DefaultCarrotSerializer();
        var messageBuilder = new CarrotMessageBuilder([], serializer, new DefaultRoutingKeyResolver());
        _carrotClient = new CarrotClient(_transport, serializer, messageBuilder);
    }

    [TestMethod]
    public async Task SendReceiveAsync_Ok()
    {
        var serializer = new DefaultCarrotSerializer();
        var okResponse = serializer.Serialize(new CarrotResponse { StatusCode = CarrotStatusCode.Ok });

        _transport.SendReceiveAsync(Arg.Any<CarrotMessage>(), CancellationToken.None)
            .ReturnsForAnyArgs(_ => Task.FromResult(new CarrotMessage { Payload = okResponse }));
        ICommand<TestDto, TestResponse, TestQueueEndPoint> request = new TestDto(1);

        CarrotResponse response = await _carrotClient.SendReceiveAsync(request).ConfigureAwait(false);

        Assert.AreEqual(CarrotStatusCode.Ok, response.StatusCode, nameof(response.StatusCode));
    }

    [TestMethod]
    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = true)]
    public async Task SendReceiveAsync_RequestTimeout_With_Ttl()
    {
        _transport.SendReceiveAsync(Arg.Any<CarrotMessage>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(
                async callInfo =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, callInfo.Arg<CancellationToken>()).ConfigureAwait(false);

                    return new CarrotMessage();
                });
        ICommand<TestDto, TestResponse, TestQueueEndPoint> request = new TestDto(1);

        await _carrotClient.SendReceiveAsync(request, messageProperties: new MessageProperties { Ttl = 1 });
    }

    [TestMethod]
    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = true)]
    public async Task SendReceiveAsync_RequestTimeout()
    {
        _transport.SendReceiveAsync(Arg.Any<CarrotMessage>(), CancellationToken.None)
            .ReturnsForAnyArgs<Task<CarrotMessage>>(_ => throw new OperationCanceledException());
        ICommand<TestDto, TestResponse, TestQueueEndPoint> request = new TestDto(1);

        await _carrotClient.SendReceiveAsync(request);
    }

    [TestMethod]
    [ExpectedException(typeof(RetryLimitExceededException))]
    public async Task SendReceiveAsync_RetryLimitException()
    {
        _transport.SendReceiveAsync(Arg.Any<CarrotMessage>(), CancellationToken.None)
            .ReturnsForAnyArgs<Task<CarrotMessage>>(_ => throw new RetryLimitExceededException());
        ICommand<TestDto, TestResponse, TestQueueEndPoint> request = new TestDto(1);

        await _carrotClient.SendReceiveAsync(request);
    }

    [TestMethod]
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public async Task SendReceiveAsync_InternalServerError()
    {
        const string exMessage = "my very important message";
        _transport.SendReceiveAsync(Arg.Any<CarrotMessage>(), CancellationToken.None)
            .ReturnsForAnyArgs<Task<CarrotMessage>>(_ => throw new Exception(exMessage));
        ICommand<TestDto, TestResponse, TestQueueEndPoint> request = new TestDto(1);

        await _carrotClient.SendReceiveAsync(request);
    }

    [TestMethod]
    public async Task SendAsync_Ok()
    {
        _transport.SendAsync(Arg.Any<CarrotMessage>(), CancellationToken.None)
            .ReturnsForAnyArgs(_ => Task.FromResult(new CarrotMessage()));
        ICommand<TestDto, TestResponse, TestQueueEndPoint> request = new TestDto(1);

        await _carrotClient.SendAsync(request);
    }

    [TestMethod]
    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = true)]
    public async Task SendAsync_RequestTimeout_With_Ttl()
    {
        _transport.SendAsync(Arg.Any<CarrotMessage>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(
                async callInfo => { await Task.Delay(Timeout.InfiniteTimeSpan, callInfo.Arg<CancellationToken>()).ConfigureAwait(false); });
        ICommand<TestDto, TestResponse, TestQueueEndPoint> request = new TestDto(1);

        await _carrotClient.SendAsync(request, messageProperties: new MessageProperties { Ttl = 1 });
    }

    [TestMethod]
    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = true)]
    public async Task SendAsync_RequestTimeout()
    {
        _transport.SendAsync(Arg.Any<CarrotMessage>(), CancellationToken.None)
            .ReturnsForAnyArgs(_ => throw new OperationCanceledException());
        ICommand<TestDto, TestResponse, TestQueueEndPoint> request = new TestDto(1);

        await _carrotClient.SendAsync(request);
    }

    [TestMethod]
    [ExpectedException(typeof(RetryLimitExceededException))]
    public async Task SendAsync_RetryLimitException()
    {
        _transport.SendAsync(Arg.Any<CarrotMessage>(), CancellationToken.None)
            .ReturnsForAnyArgs(_ => throw new RetryLimitExceededException());
        ICommand<TestDto, TestResponse, TestQueueEndPoint> request = new TestDto(1);

        await _carrotClient.SendAsync(request);
    }

    [TestMethod]
    [ExpectedException(typeof(Exception))]
    public async Task SendAsync_InternalServerError()
    {
        const string exMessage = "my very important message";
        _transport.SendAsync(Arg.Any<CarrotMessage>(), CancellationToken.None).ReturnsForAnyArgs(_ => throw new Exception(exMessage));
        ICommand<TestDto, TestResponse, TestQueueEndPoint> request = new TestDto(1);

        await _carrotClient.SendAsync(request);
    }
}