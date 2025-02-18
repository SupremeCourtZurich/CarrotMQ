using CarrotMQ.Core;
using CarrotMQ.Core.EndPoints;
using CarrotMQ.Core.Handlers;
using Dto;
using Microsoft.Extensions.Hosting;

namespace Client;

public class ClientBackgroundService : BackgroundService
{
    private readonly ICarrotClient _carrotClient;

    public ClientBackgroundService(ICarrotClient carrotClient, ResponseSubscription<MyQuery, MyQuery.Response> responseSubscription, EventSubscription<MyCustomRoutingEvent> eventSubscription)
    {
        _carrotClient = carrotClient;

        responseSubscription.ResponseReceived += async (sender, response) =>
        {
            Console.WriteLine($"Response received: {response.Response.Content?.ResponseMessage}");
            await Task.CompletedTask;
        };

        eventSubscription.EventReceived += async (sender, e) =>
        {
            Console.WriteLine($"Custom routing event received: {e.Event?.Message}");
            await Task.CompletedTask;
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var i = 0;
        while (stoppingToken.IsCancellationRequested == false)
        {
            await _carrotClient.PublishAsync(new MyCustomRoutingEvent { Exchange  = Program.ClientExchange, RoutingKey = Program.CustomRoutingKey.ToString(), Message = $"Hello World event {++i}" }, cancellationToken: CancellationToken.None);
            await _carrotClient.SendAsync(new MyQuery { Message = $"Query {i}" }, new QueueReplyEndPoint(Program.ClientQueue, true), cancellationToken: CancellationToken.None);
            await Task.Delay(10000, stoppingToken);
        }
    }
}