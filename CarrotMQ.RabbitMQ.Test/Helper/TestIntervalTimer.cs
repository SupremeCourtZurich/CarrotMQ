using CarrotMQ.Core.Common;

namespace CarrotMQ.RabbitMQ.Test.Helper;

public class TestIntervalTimer : IIntervalTimer
{
    public void Dispose()
    {
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public async Task FireTimedEvent(IIntervalTimer.IntervalTimerElapsedEventArgs e)
    {
        if (ElapsedAsync != null)
        {
            await ElapsedAsync.InvokeAllAsync(this, e).ConfigureAwait(false);
        }
    }

    public event AsyncEventHandler<IIntervalTimer.IntervalTimerElapsedEventArgs>? ElapsedAsync;
}