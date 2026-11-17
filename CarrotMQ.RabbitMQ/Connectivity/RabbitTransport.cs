using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CarrotMQ.Core.Configuration;
using CarrotMQ.Core.Protocol;
using CarrotMQ.Core.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client.Exceptions;

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
        var publisherChannel = await GetChannelAsync<IPublisherChannel>(
                message.Header.MessageProperties.PublisherConfirm
                    ? _brokerConnection.GetPublisherChannelWithConfirmsAsync
                    : _brokerConnection.GetPublisherChannelAsync,
                message.Header,
                cancellationToken)
            .ConfigureAwait(false);

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
        var directReplyChannel = await GetChannelAsync<IDirectReplyChannel>(
                message.Header.MessageProperties.PublisherConfirm
                    ? _brokerConnection.GetDirectReplyConfirmChannelAsync
                    : _brokerConnection.GetDirectReplyChannelAsync,
                message.Header,
                cancellationToken)
            .ConfigureAwait(false);

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

    // A connect timeout surfaces as BrokerUnreachableException or as OperationCanceledException.
    private async Task<TChannel> GetChannelAsync<TChannel>(
        Func<Task<TChannel>> getChannel,
        CarrotHeader header,
        CancellationToken token)
    {
        try
        {
            return await WaitAsync(getChannel(), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (token.IsCancellationRequested)
        {
            var reason = PublishFailure.ReasonAtCancellation(_brokerConnection.CurrentConnection, PublishFailureReason.Unknown);

            throw PublishFailure.Cancelled(header, reason, ex);
        }
        catch (Exception ex) when (ex is BrokerUnreachableException or OperationCanceledException)
        {
            throw PublishFailure.Unreachable(header, ex);
        }
    }

    // The channel is shared, so its creation goes on when the caller stops waiting.
    private static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken token)
    {
        if (task.IsCompleted || !token.CanBeCanceled) return await task.ConfigureAwait(false);

        var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (token.Register(() => cancelled.TrySetResult(true)))
        {
            if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task)
            {
                _ = task.ContinueWith(
                    static t => t.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                throw new OperationCanceledException(token);
            }
        }

        return await task.ConfigureAwait(false);
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