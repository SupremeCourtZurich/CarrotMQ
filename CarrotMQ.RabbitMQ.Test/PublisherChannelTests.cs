using CarrotMQ.Core;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using RabbitMQ.Client;
using IChannel = RabbitMQ.Client.IChannel;

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class PublisherChannelTests
{
    private BasicProperties _basicProperties = null!;
    private IPublisherChannel _channel = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        var channel = Substitute.For<IChannel>();
        channel.IsOpen.Returns(true);

        channel.When(
                m => m.BasicPublishAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<bool>(),
                    Arg.Any<BasicProperties>(),
                    Arg.Any<ReadOnlyMemory<byte>>(),
                    Arg.Any<CancellationToken>()))
            .Do(a => { _basicProperties = a.Arg<BasicProperties>(); });

        var connection = Substitute.For<IConnection>();
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>())
            .Returns(_ => Task.FromResult(channel));

        using var loggerFactory = LoggerFactory.Create(
            builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddConsole();
            });
        _channel = await PublisherChannel.CreateAsync(
                connection,
                TimeSpan.FromSeconds(2),
                new ProtocolSerializer(),
                loggerFactory)
            .ConfigureAwait(false);
    }

    [TestMethod]
    public async Task BasicProperties_ContentType()
    {
        var header = new CarrotHeader();
        await _channel.PublishAsync(new CarrotMessage(header, string.Empty), CancellationToken.None);

        Assert.AreEqual("application/json", _basicProperties.ContentType);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task BasicProperties_Persistent(bool persistent)
    {
        var header = new CarrotHeader { MessageProperties = new MessageProperties { Persistent = persistent } };
        await _channel.PublishAsync(new CarrotMessage(header, string.Empty), CancellationToken.None);

        Assert.AreEqual(persistent, _basicProperties.Persistent);
    }

    [TestMethod]
    [DataRow((byte)0)]
    [DataRow((byte)1)]
    [DataRow((byte)9)]
    public async Task BasicProperties_Priority(byte priority)
    {
        var header = new CarrotHeader { MessageProperties = new MessageProperties { Priority = priority } };
        await _channel.PublishAsync(new CarrotMessage(header, string.Empty), CancellationToken.None);

        Assert.AreEqual(priority, _basicProperties.Priority);
    }

    [TestMethod]
    [DataRow("a13df78d-d615-46d7-9011-755ee983b061")]
    [DataRow(null)]
    public async Task BasicProperties_CorrelationId(string? id)
    {
        Guid? correlationId = null;
        if (Guid.TryParse(id, out var parsedId)) correlationId = parsedId;
        var header = new CarrotHeader { CorrelationId = correlationId };
        await _channel.PublishAsync(new CarrotMessage(header, string.Empty), CancellationToken.None);

        Assert.AreEqual(id, _basicProperties.CorrelationId);
    }

    [TestMethod]
    [DataRow(2000, "2000")]
    [DataRow(0, "0")]
    [DataRow(-1, null)]
    [DataRow(null, null)]
    public async Task BasicProperties_Expiration(int? ttl, string? result)
    {
        var header = new CarrotHeader { MessageProperties = new MessageProperties { Ttl = ttl } };
        await _channel.PublishAsync(new CarrotMessage(header, string.Empty), CancellationToken.None);

        Assert.AreEqual(result, _basicProperties.Expiration);
    }

    [TestMethod]
    public async Task BasicProperties_MessageId()
    {
        var messageId = Guid.Empty;
        var header = new CarrotHeader();
        await _channel.PublishAsync(new CarrotMessage(header, string.Empty), CancellationToken.None);

        Assert.AreEqual(messageId.ToString(), _basicProperties.MessageId);
    }

    [TestMethod]
    public async Task BasicProperties_AppId()
    {
        var appId = Guid.Empty;
        var header = new CarrotHeader { ServiceInstanceId = appId };
        await _channel.PublishAsync(new CarrotMessage(header, string.Empty), CancellationToken.None);

        Assert.AreEqual(appId.ToString(), _basicProperties.AppId);
    }
}