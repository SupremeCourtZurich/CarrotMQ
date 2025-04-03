using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// Represents a RabbitMQ <see cref="ITransport" /> implementation for sending and receiving <see cref="CarrotMessage" />
/// instances.
/// </summary>
public sealed class RabbitTransport : ITransport
{
    private readonly IBrokerConnection _brokerConnection;
    private readonly IOptions<CarrotTracingOptions> _carrotTracingOptions;
    private readonly ILogger<RabbitTransport> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitTransport" /> class.
    /// </summary>
    /// <param name="brokerConnection">The RabbitMQ broker connection.</param>
    /// <param name="carrotTracingOptions">
    /// Options for configuring <see cref="Activity" /> tracing for published and received
    /// messages
    /// </param>
    /// <param name="logger">Logger</param>
    public RabbitTransport(
        IBrokerConnection brokerConnection,
        IOptions<CarrotTracingOptions> carrotTracingOptions,
        ILogger<RabbitTransport> logger)
    {
        _brokerConnection = brokerConnection ?? throw new ArgumentNullException(nameof(brokerConnection));
        _carrotTracingOptions = carrotTracingOptions ?? throw new ArgumentNullException(nameof(carrotTracingOptions));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(CarrotMessage message, CancellationToken cancellationToken)
    {
        var publisherChannel = message.Header.MessageProperties.PublisherConfirm
            ? await _brokerConnection.GetPublisherChannelWithConfirmsAsync().ConfigureAwait(false)
            : await _brokerConnection.GetPublisherChannelAsync().ConfigureAwait(false);

        SetHeaderInfos(message.Header);

        using var activity = CarrotActivityFactory.CreateProducerActivity(
            message.Header,
            _brokerConnection.ServiceName,
            _brokerConnection.VHost,
            _carrotTracingOptions);

        _logger.LogDebug("SendAsync {CalledMethod}", message.Header.CalledMethod);

        await publisherChannel.PublishAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CarrotMessage> SendReceiveAsync(
        CarrotMessage message,
        CancellationToken cancellationToken)
    {
        var directReplyChannel = message.Header.MessageProperties.PublisherConfirm
            ? await _brokerConnection.GetDirectReplyConfirmChannelAsync().ConfigureAwait(false)
            : await _brokerConnection.GetDirectReplyChannelAsync().ConfigureAwait(false);

        SetHeaderInfos(message.Header);

        using var activity = CarrotActivityFactory.CreateProducerActivity(
            message.Header,
            _brokerConnection.ServiceName,
            _brokerConnection.VHost,
            _carrotTracingOptions);

        var response = await directReplyChannel
            .PublishWithReplyAsync(message, cancellationToken)
            .ConfigureAwait(false);

        return response;
    }

    private void SetHeaderInfos(CarrotHeader header)
    {
        header.InitialUserName ??= _brokerConnection.UserName;
        header.InitialServiceName ??= _brokerConnection.ServiceName;
        header.ServiceName = _brokerConnection.ServiceName;
        header.ServiceInstanceId = _brokerConnection.ServiceInstanceId;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return default;
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}