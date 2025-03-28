using System.Text.Json;
using System.Threading.Channels;
using CarrotMQ.Core.MessageProcessing.Delivery;
using CarrotMQ.Core.Protocol;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Serialization;
using CarrotMQ.RabbitMQ.Test.Integration.Handlers;
using Microsoft.Extensions.Logging;
using Channel = System.Threading.Channels.Channel;

namespace CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

public sealed class DeadLetterConsumer : IAsyncDisposable
{
    private readonly IBrokerConnection _brokerConnection;
    private readonly ILogger<DeadLetterConsumer> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Channel<int> _messageChannel = Channel.CreateBounded<int>(100);
    private readonly ProtocolSerializer _protocolSerializer;
    private IConsumerChannel? _consumerChannel;

    public DeadLetterConsumer(IBrokerConnection brokerConnection, ILoggerFactory loggerFactory, ILogger<DeadLetterConsumer> logger)
    {
        _brokerConnection = brokerConnection;
        _loggerFactory = loggerFactory;
        _logger = logger;
        _protocolSerializer = new ProtocolSerializer();
    }

    public async Task InitializeAsync(string queueName, string exchangeName)
    {
        var connection = await _brokerConnection.ConnectAsync().ConfigureAwait(false);
        _consumerChannel = await ConsumerChannel.CreateAsync(
                connection,
                _brokerConnection.NetworkRecoveryInterval,
                _protocolSerializer,
                new BasicPropertiesMapper(),
                _loggerFactory)
            .ConfigureAwait(false);
        var arguments = new Dictionary<string, object?> { { "x-queue-type", "quorum" } };
        await _consumerChannel.DeclareQueueAsync(queueName, true, false, false, arguments).ConfigureAwait(false);
        await _consumerChannel.BindQueueAsync(queueName, exchangeName, string.Empty).ConfigureAwait(false);

        await _consumerChannel.StartConsumingAsync(queueName, 0, 1, ConsumingAsyncCallback).ConfigureAwait(false);
    }

    private async Task<DeliveryStatus> ConsumingAsyncCallback(CarrotMessage carrotMessage)
    {
        var payload = carrotMessage.Payload ?? string.Empty;
        _logger.LogInformation(payload);
        var msgWithId = JsonSerializer.Deserialize<DtoBase>(payload)!;

        await _messageChannel.Writer.WriteAsync(msgWithId.Id, CancellationToken.None).ConfigureAwait(false);

        return DeliveryStatus.Ack;
    }

    public async ValueTask<int> ReadAsync(CancellationToken cancellationToken)
    {
        return await _messageChannel.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_consumerChannel != null)
        {
            await _consumerChannel.DisposeAsync().ConfigureAwait(false);
        }
    }
}