using CarrotMQ.Core.Dto;
using CarrotMQ.Core.MessageProcessing;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Serialization;
using NSubstitute;

namespace CarrotMQ.Core.Test;

[TestClass]
public class CarrotClientResponseTests
{
    private ICarrotClient _carrotClient = default!;
    private ITransport _transport = default!;

    [TestInitialize]
    public void Initialize()
    {
        _transport = Substitute.For<ITransport>();
        ICarrotSerializer serializer = new DefaultCarrotSerializer();
        _carrotClient = new CarrotClient(
            [],
            _transport,
            new DefaultRoutingKeyResolver(),
            serializer);
    }

    [TestMethod]
    public async Task SendReceiveAsync_Ok()
    {
        var serializer = new DefaultCarrotSerializer();
        var okResponse = serializer.Serialize(new CarrotResponse { StatusCode = CarrotStatusCode.Ok });

        _transport.SendReceiveAsync(Arg.Any<CarrotMessage>(), default)
            .ReturnsForAnyArgs(_ => Task.FromResult(new CarrotMessage { Payload = okResponse }));
        ICommand<MyDto, TestResponse, TestQueue> request = new MyDto(1);

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
        ICommand<MyDto, TestResponse, TestQueue> request = new MyDto(1);

        var context = new Context(1);
        await _carrotClient.SendReceiveAsync(request, context);
    }

    [TestMethod]
    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = true)]
    public async Task SendReceiveAsync_RequestTimeout()
    {
        _transport.SendReceiveAsync(Arg.Any<CarrotMessage>(), default)
            .ReturnsForAnyArgs<Task<CarrotMessage>>(_ => throw new OperationCanceledException());
        ICommand<MyDto, TestResponse, TestQueue> request = new MyDto(1);

        await _carrotClient.SendReceiveAsync(request);
    }

    [TestMethod]
    [ExpectedException(typeof(RetryLimitExceededException))]
    public async Task SendReceiveAsync_RetryLimitException()
    {
        _transport.SendReceiveAsync(Arg.Any<CarrotMessage>(), default)
            .ReturnsForAnyArgs<Task<CarrotMessage>>(_ => throw new RetryLimitExceededException());
        ICommand<MyDto, TestResponse, TestQueue> request = new MyDto(1);

        await _carrotClient.SendReceiveAsync(request);
    }

    [TestMethod]
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public async Task SendReceiveAsync_InternalServerError()
    {
        const string exMessage = "my very important message";
        _transport.SendReceiveAsync(Arg.Any<CarrotMessage>(), default)
            .ReturnsForAnyArgs<Task<CarrotMessage>>(_ => throw new Exception(exMessage));
        ICommand<MyDto, TestResponse, TestQueue> request = new MyDto(1);

        await _carrotClient.SendReceiveAsync(request);
    }

    [TestMethod]
    public async Task SendAsync_Ok()
    {
        _transport.SendAsync(Arg.Any<CarrotMessage>(), default)
            .ReturnsForAnyArgs(_ => Task.FromResult(new CarrotMessage()));
        ICommand<MyDto, TestResponse, TestQueue> request = new MyDto(1);

        await _carrotClient.SendAsync(request);
    }

    [TestMethod]
    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = true)]
    public async Task SendAsync_RequestTimeout_With_Ttl()
    {
        _transport.SendAsync(Arg.Any<CarrotMessage>(), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(
                async callInfo => { await Task.Delay(Timeout.InfiniteTimeSpan, callInfo.Arg<CancellationToken>()).ConfigureAwait(false); });
        ICommand<MyDto, TestResponse, TestQueue> request = new MyDto(1);

        var context = new Context(1);
        await _carrotClient.SendAsync(request, context: context);
    }

    [TestMethod]
    [ExpectedException(typeof(OperationCanceledException), AllowDerivedTypes = true)]
    public async Task SendAsync_RequestTimeout()
    {
        _transport.SendAsync(Arg.Any<CarrotMessage>(), default)
            .ReturnsForAnyArgs(_ => throw new OperationCanceledException());
        ICommand<MyDto, TestResponse, TestQueue> request = new MyDto(1);

        await _carrotClient.SendAsync(request);
    }

    [TestMethod]
    [ExpectedException(typeof(RetryLimitExceededException))]
    public async Task SendAsync_RetryLimitException()
    {
        _transport.SendAsync(Arg.Any<CarrotMessage>(), default)
            .ReturnsForAnyArgs(_ => throw new RetryLimitExceededException());
        ICommand<MyDto, TestResponse, TestQueue> request = new MyDto(1);

        await _carrotClient.SendAsync(request);
    }

    [TestMethod]
    [ExpectedException(typeof(Exception))]
    public async Task SendAsync_InternalServerError()
    {
        const string exMessage = "my very important message";
        _transport.SendAsync(Arg.Any<CarrotMessage>(), default).ReturnsForAnyArgs(_ => throw new Exception(exMessage));
        ICommand<MyDto, TestResponse, TestQueue> request = new MyDto(1);

        await _carrotClient.SendAsync(request);
    }
}