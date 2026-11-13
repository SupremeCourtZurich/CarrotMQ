using System.Collections.Concurrent;
using Castle.DynamicProxy;
using CarrotMQ.RabbitMQ.Configuration;
using CarrotMQ.RabbitMQ.Connectivity;
using CarrotMQ.RabbitMQ.Serialization;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

/// <summary>
/// Decorates an <see cref="IBrokerConnection" /> so tests can see publishes on its publisher confirm channels and
/// drop their confirms, as if the broker had closed the channel before the confirms arrived.
/// </summary>
/// <remarks>
/// Main channels are the ones the confirm channels publish on; recovery channels are opened with a cancellable token
/// and are not counted.
/// </remarks>
internal sealed class ChannelSpy
{
    private static readonly ProxyGenerator Generator = new();
    private readonly ConcurrentBag<ChannelInterceptor> _mainChannels = [];
    private int _mainChannelPublishes;
    private TaskCompletionSource<bool>? _recoveryHold;

    /// <summary>
    /// Holds the recovery of closed main channels until the returned scope is disposed; publishes meanwhile stay unconfirmed
    /// on the closed channel and are handed over together with the ones that were outstanding at the close.
    /// </summary>
    public IDisposable HoldMainChannelRecovery()
    {
        var hold = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Volatile.Write(ref _recoveryHold, hold);

        return new Release(() =>
        {
            Volatile.Write(ref _recoveryHold, null);
            hold.TrySetResult(true);
        });
    }

    /// <summary>
    /// Starts <paramref name="publish" /> and completes once it reached a main channel, so the next publish follows it.
    /// </summary>
    public async Task<Task<T>> PublishInOrderAsync<T>(Func<Task<T>> publish)
    {
        var before = Volatile.Read(ref _mainChannelPublishes);
        var task = publish();
        await Wait.UntilAsync(() => Volatile.Read(ref _mainChannelPublishes) > before || task.IsCompleted, "publish on a main channel")
            .ConfigureAwait(false);

        return task;
    }

    /// <inheritdoc cref="PublishInOrderAsync{T}" />
    public async Task<Task> PublishInOrderAsync(Func<Task> publish)
    {
        return await PublishInOrderAsync(async () =>
            {
                await publish().ConfigureAwait(false);

                return true;
            })
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Drops every confirm that arrives from now on on the main channels open at this moment.
    /// </summary>
    public void DropConfirmsOnOpenMainChannels()
    {
        foreach (var channel in _mainChannels) channel.DropConfirms = true;
    }

    public IBrokerConnection Decorate(
        IBrokerConnection brokerConnection,
        BrokerConnectionOptions options,
        IProtocolSerializer protocolSerializer,
        ILoggerFactory loggerFactory)
    {
        return Generator.CreateInterfaceProxyWithTarget(
            brokerConnection,
            new BrokerConnectionInterceptor(this, options, protocolSerializer, loggerFactory));
    }

    private sealed class BrokerConnectionInterceptor : IInterceptor
    {
        private readonly object _lock = new();
        private readonly ILoggerFactory _loggerFactory;
        private readonly BrokerConnectionOptions _options;
        private readonly IProtocolSerializer _protocolSerializer;
        private readonly ChannelSpy _spy;
        private Task<IDirectReplyChannel>? _directReplyConfirmChannel;
        private Task<IPublisherChannel>? _publisherConfirmChannel;

        public BrokerConnectionInterceptor(
            ChannelSpy spy,
            BrokerConnectionOptions options,
            IProtocolSerializer protocolSerializer,
            ILoggerFactory loggerFactory)
        {
            _spy = spy;
            _options = options;
            _protocolSerializer = protocolSerializer;
            _loggerFactory = loggerFactory;
        }

        public void Intercept(IInvocation invocation)
        {
            var brokerConnection = (IBrokerConnection)invocation.InvocationTarget;
            lock (_lock)
            {
                switch (invocation.Method.Name)
                {
                    case nameof(IBrokerConnection.GetPublisherChannelWithConfirmsAsync):
                        _publisherConfirmChannel ??= CreatePublisherConfirmChannelAsync(brokerConnection);
                        invocation.ReturnValue = _publisherConfirmChannel;

                        return;
                    case nameof(IBrokerConnection.GetDirectReplyConfirmChannelAsync):
                        _directReplyConfirmChannel ??= CreateDirectReplyConfirmChannelAsync(brokerConnection);
                        invocation.ReturnValue = _directReplyConfirmChannel;

                        return;
                }
            }

            invocation.Proceed();
        }

        private async Task<IPublisherChannel> CreatePublisherConfirmChannelAsync(IBrokerConnection brokerConnection)
        {
            return await PublisherConfirmChannel.CreateAsync(
                    await ConnectAsync(brokerConnection).ConfigureAwait(false),
                    brokerConnection.NetworkRecoveryInterval,
                    _options.PublisherConfirm,
                    _protocolSerializer,
                    _loggerFactory)
                .ConfigureAwait(false);
        }

        private async Task<IDirectReplyChannel> CreateDirectReplyConfirmChannelAsync(IBrokerConnection brokerConnection)
        {
            return await DirectReplyConfirmChannel.CreateAsync(
                    await ConnectAsync(brokerConnection).ConfigureAwait(false),
                    brokerConnection.NetworkRecoveryInterval,
                    _options.PublisherConfirm,
                    _protocolSerializer,
                    _loggerFactory)
                .ConfigureAwait(false);
        }

        private async Task<IConnection> ConnectAsync(IBrokerConnection brokerConnection)
        {
            var connection = await brokerConnection.ConnectAsync().ConfigureAwait(false);

            return Generator.CreateInterfaceProxyWithTarget(connection, new ConnectionInterceptor(_spy));
        }
    }

    private sealed class ConnectionInterceptor : IInterceptor
    {
        private readonly ChannelSpy _spy;

        public ConnectionInterceptor(ChannelSpy spy)
        {
            _spy = spy;
        }

        public void Intercept(IInvocation invocation)
        {
            invocation.Proceed();

            if (!string.Equals(invocation.Method.Name, nameof(IConnection.CreateChannelAsync), StringComparison.Ordinal)) return;

            var isRecoveryChannel = invocation.Arguments.OfType<CancellationToken>().Any(t => t.CanBeCanceled);
            invocation.ReturnValue = DecorateAsync((Task<IChannel>)invocation.ReturnValue!, isRecoveryChannel);
        }

        private async Task<IChannel> DecorateAsync(Task<IChannel> created, bool isRecoveryChannel)
        {
            var channel = await created.ConfigureAwait(false);
            if (isRecoveryChannel) return channel;

            var interceptor = new ChannelInterceptor(_spy);
            _spy._mainChannels.Add(interceptor);

            return Generator.CreateInterfaceProxyWithTarget(channel, interceptor);
        }
    }

    private sealed class ChannelInterceptor : IInterceptor
    {
        private readonly ChannelSpy _spy;
        private volatile bool _dropConfirms;

        public ChannelInterceptor(ChannelSpy spy)
        {
            _spy = spy;
        }

        public bool DropConfirms
        {
            set => _dropConfirms = value;
        }

        public void Intercept(IInvocation invocation)
        {
            switch (invocation.Method.Name)
            {
                case "add_" + nameof(IChannel.BasicAcksAsync):
                    var acks = (AsyncEventHandler<BasicAckEventArgs>)invocation.Arguments[0];
                    invocation.SetArgumentValue(0, new AsyncEventHandler<BasicAckEventArgs>((s, e) => _dropConfirms ? Task.CompletedTask : acks(s, e)));
                    break;
                case "add_" + nameof(IChannel.BasicNacksAsync):
                    var nacks = (AsyncEventHandler<BasicNackEventArgs>)invocation.Arguments[0];
                    invocation.SetArgumentValue(0, new AsyncEventHandler<BasicNackEventArgs>((s, e) => _dropConfirms ? Task.CompletedTask : nacks(s, e)));
                    break;
                case "add_" + nameof(IChannel.ChannelShutdownAsync):
                    var shutdown = (AsyncEventHandler<ShutdownEventArgs>)invocation.Arguments[0];
                    invocation.SetArgumentValue(0, new AsyncEventHandler<ShutdownEventArgs>(async (s, e) =>
                    {
                        if (Volatile.Read(ref _spy._recoveryHold) is { } hold) await hold.Task.ConfigureAwait(false);
                        await shutdown(s, e).ConfigureAwait(false);
                    }));
                    break;
            }

            try
            {
                invocation.Proceed();
            }
            finally
            {
                if (string.Equals(invocation.Method.Name, nameof(IChannel.BasicPublishAsync), StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref _spy._mainChannelPublishes);
                }
            }
        }
    }

    private sealed class Release : IDisposable
    {
        private readonly Action _release;

        public Release(Action release)
        {
            _release = release;
        }

        public void Dispose()
        {
            _release();
        }
    }
}