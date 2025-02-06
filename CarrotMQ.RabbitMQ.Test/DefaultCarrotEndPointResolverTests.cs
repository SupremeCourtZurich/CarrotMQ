using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Connectivity;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using RabbitMQ.Client;

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class DefaultCarrotEndPointResolverTests
{
    private readonly string _firstEndPoint = "amqp://1.1.1.1:5672";
    private readonly string _lastEndPoint = "amqp://2.2.2.2";
    private readonly string _someEndPoint = "amqp://1.2.3.4:5672";
    private IOptions<BrokerConnectionOptions> _options = null!;

    [TestInitialize]
    public void Setup()
    {
        _options = Options.Create(
            new BrokerConnectionOptions
            {
                BrokerEndPoints = new List<Uri>
                {
                    new(_firstEndPoint),
                    new(_someEndPoint),
                    new(_someEndPoint),
                    new(_someEndPoint),
                    new(_someEndPoint),
                    new(_lastEndPoint)
                }
            });
    }

    [TestMethod]
    public void AllReturnsInOrder()
    {
        _options.Value.RandomizeEndPointResolving = false;
        var resolver = new DefaultCarrotEndPointResolver(_options);

        var result = resolver.All().ToArray();

        Assert.AreEqual(_firstEndPoint, result.First().ToString());
        Assert.AreEqual($"{_lastEndPoint}:{Protocols.DefaultProtocol.DefaultPort}", result.Last().ToString());
    }

    [TestMethod]
    public void AllReturnsRandomOrder()
    {
        var rnd = Substitute.For<Random>();
        rnd.Next().Returns(6, 2, 3, 4, 5, 1); // switch first and last only
        _options.Value.RandomizeEndPointResolving = true;
        var resolver = new DefaultCarrotEndPointResolver(_options, rnd);

        var result = resolver.All().ToArray();

        Assert.AreEqual($"{_lastEndPoint}:{Protocols.DefaultProtocol.DefaultPort}", result.First().ToString());
        Assert.AreEqual(_firstEndPoint, result.Last().ToString());
    }
}