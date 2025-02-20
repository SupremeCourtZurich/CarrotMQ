using CarrotMQ.RabbitMQ.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

public class CarrotHelper : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public CarrotHelper(
        string serviceName,
        Action<CarrotConfigurationBuilder>? customConfig = null,
        Action<IServiceCollection>? serviceCollectionConfig = null,
        Action<BrokerConnectionOptions>? configureBrokerConnection = null)
    {
        var applicationBuilder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(["environment=Development"]);

        applicationBuilder.Services.AddCarrotMqRabbitMq(
            builder =>
            {
                builder.ConfigureBrokerConnection(
                    configureOptions: options =>
                    {
                        TestBase.ConfigureBroker(options);
                        options.ServiceName = serviceName;
                        configureBrokerConnection?.Invoke(options);
                    });

                customConfig?.Invoke(builder);
            });

        serviceCollectionConfig?.Invoke(applicationBuilder.Services);

        Host = applicationBuilder.Build();
        _ = Host.RunAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
    }

    public IHost Host { get; }

    public async Task WaitForConsumerHostBootstrapToCompleteAsync()
    {
        await Host.WaitForConsumerHostBootstrapToCompleteAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}