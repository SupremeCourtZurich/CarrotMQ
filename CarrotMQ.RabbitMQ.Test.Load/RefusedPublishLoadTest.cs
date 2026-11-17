namespace CarrotMQ.RabbitMQ.Test.Load;

/// <summary>
/// Compares a producer that isolates refused publishes with one that retries them, under the same load.
/// </summary>
/// <remarks>
/// Runs only with <c>CARROTMQ_LOAD_TEST=1</c>. Sizes: <c>CARROTMQ_LOAD_EVENTS</c>, <c>CARROTMQ_LOAD_QUERIES</c>,
/// <c>CARROTMQ_LOAD_IN_FLIGHT</c>, <c>CARROTMQ_LOAD_BLOCKS</c>, <c>CARROTMQ_LOAD_ROUND_TIMEOUT_S</c>.
/// </remarks>
[TestClass]
[TestCategory("Load")]
public class RefusedPublishLoadTest
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Timeout(3_600_000)]
    public async Task IsolatedVersusRetried_ReportsRatios()
    {
        // Arrange
        LoadBroker.SkipUnlessEnabled();
        var settings = ReadSettings();
        using var clientErrors = new ClientErrorListener();
        var responder = await Responder.StartAsync().ConfigureAwait(false);
        await using var responderScope = responder.ConfigureAwait(false);
        var retried = await Producer.StartAsync(isolateRefusedPublishes: false).ConfigureAwait(false);
        await using var retriedScope = retried.ConfigureAwait(false);
        var isolated = await Producer.StartAsync(isolateRefusedPublishes: true).ConfigureAwait(false);
        await using var isolatedScope = isolated.ConfigureAwait(false);

        // Act
        var comparisons = new List<Comparison>();
        foreach (var scenario in Scenarios(settings))
        {
            comparisons.Add(await Comparison.RunAsync(retried, isolated, scenario, settings).ConfigureAwait(false));
        }

        Publish(LoadReport.Write(comparisons, settings) + CloseReasons(retried) + CloseReasons(isolated) + ClientErrors(clientErrors));

        // Assert: a query confirmed just before a refusal closes its channel loses its reply, so only events must all arrive.
        foreach (var comparison in comparisons.Where(c => c.Scenario.Kind == MessageKind.Event))
        {
            Assert.AreEqual(0, comparison.IsolatedGoodLost, $"{comparison.Scenario.Name}: good events failed or unfinished with isolation");
        }
    }

    private static LoadSettings ReadSettings()
    {
        return new LoadSettings(
            LoadBroker.Setting("CARROTMQ_LOAD_EVENTS", 2_000),
            LoadBroker.Setting("CARROTMQ_LOAD_QUERIES", 500),
            LoadBroker.Setting("CARROTMQ_LOAD_IN_FLIGHT", 100),
            LoadBroker.Setting("CARROTMQ_LOAD_BLOCKS", 2),
            TimeSpan.FromSeconds(LoadBroker.Setting("CARROTMQ_LOAD_ROUND_TIMEOUT_S", 120)));
    }

    private static IEnumerable<Scenario> Scenarios(LoadSettings settings)
    {
        var only = Environment.GetEnvironmentVariable("CARROTMQ_LOAD_SCENARIO");
        Scenario[] all =
        [
            new("Events, none refused", MessageKind.Event, settings.Events, 0),
            new("Events, 0.1% refused", MessageKind.Event, settings.Events, 1_000),
            new("Events, 1% refused", MessageKind.Event, settings.Events, 100),
            new("Events, 10% refused", MessageKind.Event, settings.Events, 10),
            new("Queries, 1% refused", MessageKind.Query, settings.Queries, 100)
        ];

        return string.IsNullOrEmpty(only) ? all : all.Where(s => s.Name.Contains(only, StringComparison.OrdinalIgnoreCase));
    }

    private static string CloseReasons(Producer producer)
    {
        var variant = producer.IsolateRefusedPublishes ? "isolated" : "retried";
        var lines = producer.Closes.Reasons.OrderByDescending(r => r.Value).Select(r => $"- {variant}: {r.Value} × {r.Key}");

        return $"{Environment.NewLine}Close reasons, {variant}:{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}{Environment.NewLine}";
    }

    private static string ClientErrors(ClientErrorListener listener)
    {
        var lines = listener.Errors.OrderByDescending(e => e.Value).Select(e => $"- {e.Value} × {e.Key}");

        return $"{Environment.NewLine}RabbitMQ.Client errors:{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}{Environment.NewLine}";
    }

    private void Publish(string report)
    {
        TestContext.WriteLine(report);
        var directory = TestContext.TestRunResultsDirectory ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "load-report.md"), report);
    }
}
