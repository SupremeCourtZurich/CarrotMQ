using System.Collections.Concurrent;

namespace CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

public class AsyncBarrier
{
    private readonly int _participantCount;
    private int _remainingParticipants;
    private ConcurrentStack<TaskCompletionSource<bool>> _waiters;

    public AsyncBarrier(int participantCount)
    {
        if (participantCount <= 0) throw new ArgumentOutOfRangeException(nameof(participantCount));
        _remainingParticipants = _participantCount = participantCount;
        _waiters = new ConcurrentStack<TaskCompletionSource<bool>>();
    }

    public Task SignalAndWaitAsync(CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<bool>();
        cancellationToken.Register(() => tcs.TrySetCanceled());
        _waiters.Push(tcs);
        if (Interlocked.Decrement(ref _remainingParticipants) == 0)
        {
            _remainingParticipants = _participantCount;
            var waiters = _waiters;
            _waiters = new ConcurrentStack<TaskCompletionSource<bool>>();
            Parallel.ForEach(waiters, w => w.SetResult(true));
        }
        return tcs.Task;
    }
}