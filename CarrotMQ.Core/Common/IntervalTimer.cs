using System;
using System.Timers;

namespace CarrotMQ.Core.Common;

/// <summary>
/// Timer that fires a recurring <see cref="ElapsedAsync">Elapsed event</see> after a given interval.
/// The interval restarts only after the event has been "processed".
/// It is based on the <see cref="Timer">System.Timers.Timer</see>.
/// </summary>
public sealed class IntervalTimer : IIntervalTimer
{
    private readonly Timer _timer;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntervalTimer" /> class with the specified interval.
    /// </summary>
    /// <param name="intervalInMs">The interval, in milliseconds, at which the timer elapses.</param>
    public IntervalTimer(uint intervalInMs)
    {
        _timer = new Timer(intervalInMs)
        {
            AutoReset = true,
            Enabled = false
        };
        _timer.Elapsed += TimerOnElapsed;
    }

    private async void TimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        _timer.Stop();
        try
        {
            if (ElapsedAsync != null)
            {
                await ElapsedAsync.InvokeAllAsync(this, new IIntervalTimer.IntervalTimerElapsedEventArgs(e)).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // Ignore
        }

        _timer.Start();
    }

    /// <inheritdoc />
    public void Start()
    {
        _timer.Start();
    }

    /// <inheritdoc />
    public void Stop()
    {
        _timer.Stop();
    }

    /// <inheritdoc />
    public event AsyncEventHandler<IIntervalTimer.IntervalTimerElapsedEventArgs>? ElapsedAsync;

    /// <inheritdoc />
    public void Dispose()
    {
        _timer.Dispose();
    }
}