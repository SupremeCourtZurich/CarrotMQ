using System.Diagnostics;
using CarrotMQ.Core;
using CarrotMQ.Core.Protocol;

namespace CarrotMQ.RabbitMQ.Test.Load;

internal enum MessageKind
{
    Event,
    Query
}

/// <summary>
/// Sends <see cref="Messages" /> messages; every <see cref="RefusedEvery" />-th goes to a missing exchange (0: none).
/// </summary>
internal sealed record Scenario(string Name, MessageKind Kind, int Messages, int RefusedEvery)
{
    public bool IsRefused(int index)
    {
        return RefusedEvery > 0 && index % RefusedEvery == RefusedEvery - 1;
    }
}

/// <summary>
/// One round of one producer, over the good messages only. Goodput: confirmed good messages per second until the last
/// good message finished, confirmed or failed. Latencies: confirmed good messages.
/// </summary>
internal sealed record RoundResult(
    int GoodFailed,
    int GoodUnfinished,
    double ThroughputPerSecond,
    double P50Ms,
    double P99Ms,
    int ChannelCloses,
    IReadOnlyList<string> Failures);

internal static class LoadRound
{
    public static async Task<RoundResult> RunAsync(Producer producer, Scenario scenario, int inFlight, TimeSpan timeout)
    {
        var closesBefore = producer.Closes.Count;
        // Not disposed: sends still running after a timed-out round release it later.
#pragma warning disable CA2000
        var gate = new SemaphoreSlim(inFlight);
#pragma warning restore CA2000
        using var deadline = new CancellationTokenSource(timeout);
        var clock = Stopwatch.StartNew();
        var good = new List<Task<Outcome>>();
        var refused = new List<Task<Outcome>>();
        var notSent = 0;

        for (var i = 0; i < scenario.Messages; i++)
        {
            if (!await TryEnterAsync(gate, deadline.Token).ConfigureAwait(false))
            {
                notSent = scenario.Messages - i;

                break;
            }

            var isRefused = scenario.IsRefused(i);
            var started = clock.Elapsed;
            var tracked = TrackAsync(SendAsync(producer.Client, scenario.Kind, isRefused, i), gate, clock, started);
            (isRefused ? refused : good).Add(tracked);
        }

        var all = Task.WhenAll(good.Concat(refused));
        await Task.WhenAny(all, Task.Delay(Timeout.Infinite, deadline.Token)).ConfigureAwait(false);

        return Summarize(good, notSent, producer.Closes.Count - closesBefore);
    }

    private static async Task<bool> TryEnterAsync(SemaphoreSlim gate, CancellationToken deadline)
    {
        try
        {
            await gate.WaitAsync(deadline).ConfigureAwait(false);

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static Task SendAsync(ICarrotClient client, MessageKind kind, bool refused, int id)
    {
        return kind switch
        {
            MessageKind.Event => client.PublishAsync(new LoadEvent(refused ? Destinations.MissingExchange : Destinations.ExistingExchange)),
            MessageKind.Query when refused => client.SendReceiveAsync(new MissingExchangeQuery()),
            _ => QueryAsync(client, id)
        };
    }

    private static async Task QueryAsync(ICarrotClient client, int id)
    {
        var response = await client.SendReceiveAsync(new LoadQuery { Id = id }).ConfigureAwait(false);
        if (response.StatusCode != CarrotStatusCode.Ok || response.Content?.Id != id)
        {
            throw new InvalidOperationException($"Query {id} answered with {response.StatusCode}.");
        }
    }

    private static async Task<Outcome> TrackAsync(Task send, SemaphoreSlim gate, Stopwatch clock, TimeSpan started)
    {
        try
        {
            await send.ConfigureAwait(false);

            return new Outcome(true, clock.Elapsed - started, clock.Elapsed, null);
        }
        catch (Exception ex)
        {
            return new Outcome(false, clock.Elapsed - started, clock.Elapsed, Describe(ex));
        }
        finally
        {
            gate.Release();
        }
    }

    private static RoundResult Summarize(List<Task<Outcome>> good, int notSent, int channelCloses)
    {
        var finished = good.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result).ToList();
        var confirmed = finished.Where(o => o.Confirmed).ToList();
        var latencies = confirmed.Select(o => o.Latency.TotalMilliseconds).Order().ToList();
        var lastFinished = finished.Count == 0 ? TimeSpan.Zero : finished.Max(o => o.FinishedAt);

        return new RoundResult(
            finished.Count - confirmed.Count,
            good.Count - finished.Count + notSent,
            lastFinished > TimeSpan.Zero ? confirmed.Count / lastFinished.TotalSeconds : 0,
            Percentile(latencies, 0.50),
            Percentile(latencies, 0.99),
            channelCloses,
            finished.Where(o => !o.Confirmed).Select(o => o.Failure!).ToList());
    }

    private static string Describe(Exception exception)
    {
        return exception is PublishFailedException failed
            ? $"{exception.GetType().Name}({failed.Reason}, inner {failed.InnerException?.GetType().Name ?? "none"})"
            : exception.GetType().Name;
    }

    // Nearest-rank percentile of an ascending list.
    private static double Percentile(List<double> ascending, double percentile)
    {
        if (ascending.Count == 0) return double.NaN;

        var rank = (int)Math.Ceiling(percentile * ascending.Count);

        return ascending[Math.Clamp(rank - 1, 0, ascending.Count - 1)];
    }

    private sealed record Outcome(bool Confirmed, TimeSpan Latency, TimeSpan FinishedAt, string? Failure);
}
