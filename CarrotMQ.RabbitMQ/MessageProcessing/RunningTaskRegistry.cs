using System.Threading.Tasks;
using RabbitMQ.Client.Events;

namespace CarrotMQ.RabbitMQ.MessageProcessing;

/// <inheritdoc />
internal sealed class RunningTaskRegistry : IRunningTaskRegistry
{
#if NET9_0_OR_GREATER
    private readonly System.Threading.Lock _taskCounterLock = new();
#else
    private readonly object _taskCounterLock = new();
#endif

    private int _taskCounter;
    private TaskCompletionSource<bool>? _waitForTaskCompletion;

    /// <inheritdoc />
    public bool TryAdd(BasicDeliverEventArgs ea)
    {
        lock (_taskCounterLock)
        {
            // Allow adding new tasks as long as other tasks are still running. We assume that the consumer is disconnected and only a race condition would allow message to arrive afterward.
            if (_waitForTaskCompletion is not null && _taskCounter == 0)
            {
                return false;
            }

            _taskCounter++;

            return true;
        }
    }

    /// <inheritdoc />
    public void Remove(BasicDeliverEventArgs ea)
    {
        lock (_taskCounterLock)
        {
            _taskCounter--;

            if (_taskCounter == 0)
            {
                _waitForTaskCompletion?.SetResult(true);
            }
        }
    }

    /// <inheritdoc />
    public async Task CompleteAddingAsync()
    {
        lock (_taskCounterLock)
        {
            _waitForTaskCompletion = new TaskCompletionSource<bool>();

            if (_taskCounter == 0)
            {
                return;
            }
        }

        await _waitForTaskCompletion.Task.ConfigureAwait(false);
    }
}