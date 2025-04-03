using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class ChannelDisposeTest
{
    [TestMethod]
    [ExpectedException(typeof(ObjectDisposedException))]
    public async Task DoubleDisposeTest()
    {
        var brokerConnection = Substitute.For<IBrokerConnection>();
        using var loggerFactory = LoggerFactory.Create(
            builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddConsole();
            });

        var connection = await brokerConnection.ConnectAsync().ConfigureAwait(false);
        ICarrotChannel channel = await PublisherChannel.CreateAsync(
                connection,
                TimeSpan.FromSeconds(2),
                new ProtocolSerializer(),
                loggerFactory)
            .ConfigureAwait(false);

        var t1 = Task.Run(async () => await channel.DisposeAsync().ConfigureAwait(false));
        var t2 = Task.Run(async () => await channel.DisposeAsync().ConfigureAwait(false));

        await Task.WhenAll(t1, t2).ConfigureAwait(false);
    }
}