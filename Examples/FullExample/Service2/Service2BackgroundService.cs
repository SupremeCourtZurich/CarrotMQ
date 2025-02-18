using CarrotMQ.Core;
using CarrotMQ.Core.EndPoints;
using Dto;
using Microsoft.Extensions.Hosting;

namespace Service2;

public class Service2BackgroundService : BackgroundService
{
    private readonly ICarrotClient _carrotClient;

    public Service2BackgroundService(ICarrotClient carrotClient)
    {
        _carrotClient = carrotClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var i = 0;
        while (stoppingToken.IsCancellationRequested == false)
        {
            await _carrotClient.SendAsync(new MyCommand(){ Message = $"Command {++i}" }, new QueueReplyEndPoint(Program.ResponseQueue, true), cancellationToken: CancellationToken.None);
            await Task.Delay(10000, stoppingToken);
        }
    }
}