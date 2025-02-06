using CarrotMQ.RabbitMQ.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CarrotMQ.RabbitMQ.Test;

[TestClass]
public class HostOptionValidationExceptionTest
{
    [TestMethod]
    [ExpectedException(typeof(OptionsValidationException))]
    public async Task Test_Host_ValidationException()
    {
        var applicationBuilder = Host.CreateApplicationBuilder();

        applicationBuilder.Services.AddCarrotMqRabbitMq(
            builder => { builder.ConfigureBrokerConnection(configureOptions: options => { options.BrokerEndPoints = new List<Uri>(); }); });

        using var host = applicationBuilder
            .Build();

        using CancellationTokenSource cts = new(300);

        await host.RunAsync(cts.Token);
    }
}