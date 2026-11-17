using Testcontainers.RabbitMq;

namespace CarrotMQ.RabbitMQ.Test.Load;

/// <summary>
/// Starts one RabbitMQ container for the whole load run. Load tests run only with <c>CARROTMQ_LOAD_TEST=1</c>.
/// </summary>
[TestClass]
public static class LoadBroker
{
    public const string UserName = "loadTestUser";
    public const string Password = "loadTestPassword1";

    private static RabbitMqContainer? s_container;

    public static bool Enabled => string.Equals(Environment.GetEnvironmentVariable("CARROTMQ_LOAD_TEST"), "1", StringComparison.Ordinal);

    public static Uri AmqpEndPoint { get; private set; } = null!;

    [AssemblyInitialize]
    public static async Task StartAsync(TestContext context)
    {
        if (!Enabled) return;

        s_container = new RabbitMqBuilder("rabbitmq:4.2-management")
            .WithUsername(UserName)
            .WithPassword(Password)
            .Build();
        await s_container.StartAsync().ConfigureAwait(false);
        AmqpEndPoint = new Uri($"amqp://{s_container.Hostname}:{s_container.GetMappedPublicPort(5672)}");
    }

    [AssemblyCleanup]
    public static async Task StopAsync()
    {
        if (s_container != null) await s_container.DisposeAsync().ConfigureAwait(false);
    }

    public static void SkipUnlessEnabled()
    {
        if (!Enabled) Assert.Inconclusive("Load tests run only with CARROTMQ_LOAD_TEST=1.");
    }

    public static int Setting(string name, int defaultValue)
    {
        return int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0 ? value : defaultValue;
    }
}
