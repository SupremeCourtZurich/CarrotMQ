using System;
using System.Timers;

namespace CarrotMQ.Core.Common;

/// <summary>
/// Timer that fires a recurring <see cref="ElapsedAsync">Elapsed event</see> after a given interval
/// </summary>
public interface IIntervalTimer : IDisposable
{
    /// <summary>
    /// Starts the interval timer.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the interval timer.
    /// </summary>
    void Stop();

    /// <summary>
    /// Occurs when the interval timer elapses.
    /// </summary>
    event AsyncEventHandler<IntervalTimerElapsedEventArgs>? ElapsedAsync;

    /// <summary>
    /// Provides data for the <see cref="IIntervalTimer.ElapsedAsync" /> event.
    /// </summary>
    public class IntervalTimerElapsedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IntervalTimerElapsedEventArgs" /> class.
        /// </summary>
        public IntervalTimerElapsedEventArgs()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntervalTimerElapsedEventArgs" /> class.
        /// </summary>
        public IntervalTimerElapsedEventArgs(ElapsedEventArgs e)
        {
            ElapsedAt = e.SignalTime;
        }

        /// <summary>
        /// Date and time when the interval timer elapsed.
        /// </summary>
        public DateTime ElapsedAt { get; set; }
    }
}