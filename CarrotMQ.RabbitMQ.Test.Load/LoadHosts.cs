using CarrotMQ.Core;
using CarrotMQ.RabbitMQ.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarrotMQ.RabbitMQ.Test.Load;

/// <summary>
/// A host that publishes, with refused publishes isolated or not; counts the main-channel closes it logs.
/// </summary>
internal sealed class Producer : IAsyncDisposable
{
    private readonly IHost _host;

    private Producer(IHost host, ChannelCloseCounter closes, bool isolateRefusedPublishes)
    {
        _host = host;
        Closes = closes;
        IsolateRefusedPublishes = isolateRefusedPublishes;
        Client = host.Services.GetRequiredService<ICarrotClient>();
    }

    public ICarrotClient Client { get; }

    public ChannelCloseCounter Closes { get; }

    public bool IsolateRefusedPublishes { get; }

    public static async Task<Producer> StartAsync(bool isolateRefusedPublishes)
    {
        var closes = new ChannelCloseCounter();
        var host = LoadHost.Build(
            isolateRefusedPublishes ? "Load.Producer.Isolated" : "Load.Producer.Retried",
            options => options.PublisherConfirm.IsolateRefusedPublishes = isolateRefusedPublishes,
            _ => { },
            logging => logging.AddProvider(closes));
        await host.StartAsync().ConfigureAwait(false);

        return new Producer(host, closes, isolateRefusedPublishes);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync().ConfigureAwait(false);
        _host.Dispose();
    }
}

/// <summary>
/// A host that answers <see cref="LoadQuery" />.
/// </summary>
internal sealed class Responder : IAsyncDisposable
{
    private readonly IHost _host;

    private Responder(IHost host)
    {
        _host = host;
    }

    public static async Task<Responder> StartAsync()
    {
        var host = LoadHost.Build(
            "Load.Responder",
            options => options.ConsumerDispatchConcurrency = 20,
            builder =>
            {
                var exchange = builder.Exchanges.AddDirect<LoadExchange>();
                var queue = builder.Queues.AddClassic(Destinations.QueryQueue).WithConsumer(c => c.WithPrefetchCount(100));
                builder.Handlers.AddQuery<LoadQueryHandler, LoadQuery, LoadQuery.Response>().BindTo(exchange, queue);
                builder.StartAsHostedService();
            },
            _ => { });
        await host.StartAsync().ConfigureAwait(false);
        await host.Services.GetRequiredService<ICarrotConsumerManager>().StartConsumingAsync().ConfigureAwait(false);

        return new Responder(host);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync().ConfigureAwait(false);
        _host.Dispose();
    }
}

internal static class LoadHost
{
    // Both producers use the same retry settings, so only IsolateRefusedPublishes differs.
    public static readonly ushort RetryLimit = (ushort)LoadBroker.Setting("CARROTMQ_LOAD_RETRY_LIMIT", 5);
    public static readonly uint RetryIntervalInMs = (uint)LoadBroker.Setting("CARROTMQ_LOAD_RETRY_INTERVAL_MS", 100);

    public static IHost Build(
        string serviceName,
        Action<BrokerConnectionOptions> configureOptions,
        Action<CarrotConfigurationBuilder> configureCarrot,
        Action<ILoggingBuilder> configureLogging)
    {
        var applicationBuilder = Host.CreateApplicationBuilder(["environment=Development"]);
        applicationBuilder.Logging.ClearProviders();
        applicationBuilder.Logging.SetMinimumLevel(LogLevel.Information);
        configureLogging(applicationBuilder.Logging);

        applicationBuilder.Services.AddCarrotMqRabbitMq(builder =>
        {
            builder.ConfigureBrokerConnection(
                configureOptions: options =>
                {
                    options.BrokerEndPoints = [LoadBroker.AmqpEndPoint];
                    options.UserName = LoadBroker.UserName;
                    options.Password = LoadBroker.Password;
                    options.VHost = "/";
                    options.ServiceName = serviceName;
                    options.InitialConnectionTimeout = TimeSpan.FromSeconds(30);
                    options.PublisherConfirm.RetryLimit = RetryLimit;
                    options.PublisherConfirm.RetryIntervalInMs = RetryIntervalInMs;
                    configureOptions(options);
                });
            configureCarrot(builder);
        });

        return applicationBuilder.Build();
    }
}

/// <summary>
/// Counts the "Channel shutdown" messages a host logs, one per closed main channel.
/// </summary>
internal sealed class ChannelCloseCounter : ILoggerProvider
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _reasons = new(StringComparer.Ordinal);
    private int _count;

    public int Count => Volatile.Read(ref _count);

    /// <summary>
    /// How often each channel or connection close reason was logged.
    /// </summary>
    public IReadOnlyDictionary<string, int> Reasons => _reasons;

    public ILogger CreateLogger(string categoryName)
    {
        return new Logger(this);
    }

    public void Dispose()
    {
    }

    private sealed class Logger(ChannelCloseCounter counter) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (message.StartsWith("Channel shutdown.", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref counter._count);
                counter._reasons.AddOrUpdate(message, 1, (_, n) => n + 1);
            }
            else if (message.StartsWith("Connection shut down.", StringComparison.Ordinal))
            {
                counter._reasons.AddOrUpdate(message.ReplaceLineEndings(" "), 1, (_, n) => n + 1);
            }
        }
    }
}
