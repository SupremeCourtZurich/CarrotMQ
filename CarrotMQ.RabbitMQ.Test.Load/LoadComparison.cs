using System.Globalization;
using System.Text;

namespace CarrotMQ.RabbitMQ.Test.Load;

internal sealed record LoadSettings(int Events, int Queries, int InFlight, int Blocks, TimeSpan RoundTimeout);

/// <summary>
/// The rounds of one scenario for both producers; round i of one is paired with round i of the other.
/// </summary>
internal sealed record Comparison(Scenario Scenario, IReadOnlyList<RoundResult> Retried, IReadOnlyList<RoundResult> Isolated)
{
    // One warm-up round each, then blocks of retried, isolated, isolated, retried to even out drift.
    public static async Task<Comparison> RunAsync(Producer retried, Producer isolated, Scenario scenario, LoadSettings settings)
    {
        await LoadRound.RunAsync(retried, scenario, settings.InFlight, settings.RoundTimeout).ConfigureAwait(false);
        await LoadRound.RunAsync(isolated, scenario, settings.InFlight, settings.RoundTimeout).ConfigureAwait(false);

        var retriedRounds = new List<RoundResult>();
        var isolatedRounds = new List<RoundResult>();
        for (var block = 0; block < settings.Blocks; block++)
        {
            foreach (var producer in new[] { retried, isolated, isolated, retried })
            {
                var result = await LoadRound.RunAsync(producer, scenario, settings.InFlight, settings.RoundTimeout).ConfigureAwait(false);
                (producer.IsolateRefusedPublishes ? isolatedRounds : retriedRounds).Add(result);
            }
        }

        return new Comparison(scenario, retriedRounds, isolatedRounds);
    }

    public int IsolatedGoodLost => Isolated.Sum(r => r.GoodFailed + r.GoodUnfinished);

    public string ToRow()
    {
        return string.Join(
            " | ",
            $"| {Scenario.Name}",
            Ratio(r => r.ThroughputPerSecond),
            Ratio(r => r.P50Ms),
            Ratio(r => r.P99Ms),
            Counts(r => r.GoodFailed),
            Counts(r => r.GoodUnfinished),
            Counts(r => r.ChannelCloses) + " |");
    }

    // Median and range of isolated ÷ retried over the paired rounds.
    private string Ratio(Func<RoundResult, double> metric)
    {
        var ratios = Retried.Zip(Isolated, (r, i) => metric(i) / metric(r))
            .Where(ratio => !double.IsNaN(ratio) && !double.IsInfinity(ratio))
            .Order()
            .ToList();
        if (ratios.Count == 0) return "n/a";

        var median = ratios.Count % 2 == 1
            ? ratios[ratios.Count / 2]
            : (ratios[(ratios.Count / 2) - 1] + ratios[ratios.Count / 2]) / 2;

        return string.Create(CultureInfo.InvariantCulture, $"{median:0.00} ({ratios[0]:0.00}–{ratios[^1]:0.00})");
    }

    public IEnumerable<string> FailureLines()
    {
        foreach (var (variant, rounds) in new[] { ("retried", Retried), ("isolated", Isolated) })
        {
            foreach (var group in rounds.SelectMany(r => r.Failures).GroupBy(f => f, StringComparer.Ordinal).OrderByDescending(g => g.Count()))
            {
                yield return string.Create(CultureInfo.InvariantCulture, $"- {Scenario.Name}, {variant}: {group.Count()} × {group.Key}");
            }
        }
    }

    private string Counts(Func<RoundResult, int> metric)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{Retried.Sum(metric)} / {Isolated.Sum(metric)}");
    }
}

internal static class LoadReport
{
    public static string Write(IEnumerable<Comparison> comparisons, LoadSettings settings)
    {
        var report = new StringBuilder();
        report.AppendLine("## Refused publishes: isolated ÷ retried");
        report.AppendLine();
        report.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{settings.Events} events or {settings.Queries} queries per round, {settings.InFlight} in flight, {settings.Blocks * 2} rounds per producer after one warm-up. "
            + $"Both producers: RetryLimit {LoadHost.RetryLimit}, RetryIntervalInMs {LoadHost.RetryIntervalInMs}; only IsolateRefusedPublishes differs."));
        report.AppendLine("Good events go unrouted to amq.direct (confirmed without storage); refused ones to a missing exchange. "
            + "Queries are answered by a separate host; their TTL is the default 5 s.");
        report.AppendLine();
        report.AppendLine("Ratios are median (min–max) over paired rounds: goodput above 1 and latency below 1 favour isolation. "
            + "Goodput is confirmed good messages per second until the last good message finished; latencies cover confirmed good messages only. "
            + "Counts are totals over all rounds, retried / isolated.");
        report.AppendLine();
        report.AppendLine("| Scenario | Goodput | p50 latency | p99 latency | Good failed | Good unfinished | Main-channel closes |");
        report.AppendLine("|---|---|---|---|---|---|---|");
        var all = comparisons.ToList();
        foreach (var comparison in all)
        {
            report.AppendLine(comparison.ToRow());
        }

        report.AppendLine();
        report.AppendLine("Failures of good messages:");
        report.AppendLine();
        foreach (var line in all.SelectMany(c => c.FailureLines()))
        {
            report.AppendLine(line);
        }

        return report.ToString();
    }
}
