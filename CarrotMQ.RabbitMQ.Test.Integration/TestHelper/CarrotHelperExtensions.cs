using CarrotMQ.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

internal static class CarrotHelperExtensions
{
    public static async Task WaitForConsumerHostBootstrapToCompleteAsync(this IHost host)
    {
        var carrotConsumerManager = host.Services.GetRequiredService<ICarrotConsumerManager>();
        await carrotConsumerManager.StartConsumingAsync().ConfigureAwait(false);
    }
}