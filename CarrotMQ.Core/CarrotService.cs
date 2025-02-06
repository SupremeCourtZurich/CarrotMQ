using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace CarrotMQ.Core;

/// <summary>
/// Represents the main message consumer service.
/// </summary>
public sealed class CarrotService : BackgroundService
{
    private readonly ICarrotConsumerManager _carrotConsumerManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CarrotService" /> class.
    /// </summary>
    /// <param name="carrotConsumerManager">The transport layer.</param>
    public CarrotService(ICarrotConsumerManager carrotConsumerManager)
    {
        _carrotConsumerManager = carrotConsumerManager;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _carrotConsumerManager.StartConsumingAsync().ConfigureAwait(false);
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            // Cancellation Requested
        }

        await _carrotConsumerManager.StopConsumingAsync().ConfigureAwait(false);
    }
}