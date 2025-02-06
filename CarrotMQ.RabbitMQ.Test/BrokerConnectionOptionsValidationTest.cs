using CarrotMQ.RabbitMQ.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class BrokerConnectionOptionsValidationTest
{
    private readonly BrokerConnectionOptionsValidation _validation = new();
    private BrokerConnectionOptions _options = null!;

    [TestInitialize]
    public void Setup()
    {
        _options = new BrokerConnectionOptions
        {
            BrokerEndPoints = { new Uri("amqp://127.0.0.1:5672") },
            VHost = "E",
            UserName = "Hello",
            ServiceName = "TestService"
        };
    }

    [TestMethod]
    public void BrokerConnectionOptionsValidation_Ok_Test()
    {
        var result = _validation.Validate(null, _options);

        Assert.IsTrue(result.Succeeded);
    }

    [TestMethod]
    public void BrokerConnectionOptionsValidation_Fail_NoBrokerEndPoint_Test()
    {
        _options.BrokerEndPoints = new List<Uri>();

        var result = _validation.Validate(null, _options);

        Assert.IsTrue(result.Failed);
    }

    [TestMethod]
    public void BrokerConnectionOptionsValidation_Fail_NullBrokerEndPoint_Test()
    {
        _options.BrokerEndPoints = null!;

        var result = _validation.Validate(null, _options);

        Assert.IsTrue(result.Failed);
    }

    [TestMethod]
    public void BrokerConnectionOptionsValidation_Fail_EmptyVhost_Test()
    {
        _options.VHost = " ";

        var result = _validation.Validate(null, _options);

        Assert.IsTrue(result.Failed);
    }

    [TestMethod]
    public void BrokerConnectionOptionsValidation_Fail_NoPublisherConfirmOptions_Test()
    {
        _options.PublisherConfirm = null!;

        var result = _validation.Validate(null, _options);

        Assert.IsTrue(result.Failed);
    }
}