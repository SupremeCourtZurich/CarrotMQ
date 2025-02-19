using CarrotMQ.Core;
using Dto;
using Microsoft.Extensions.Hosting;

namespace Service1;

public class Service1BackgroundService : BackgroundService
{
    private readonly ICarrotClient _carrotClient;

    public Service1BackgroundService(ICarrotClient carrotClient)
    {
        _carrotClient = carrotClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var i = 0;
        while (stoppingToken.IsCancellationRequested == false)
        {
            await _carrotClient.PublishAsync(new MyEvent { Message = $"Event {++i}" }, cancellationToken: CancellationToken.None);
            await Task.Delay(10000, stoppingToken);
        }
    }
}