using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarrotMQ.RabbitMQ.Connectivity;

/// <summary>
/// Waits for a task that others may share, so it goes on when the caller stops waiting.
/// </summary>
internal static class CancellableWait
{
    /// <summary>
    /// Waits for <paramref name="task" /> until <paramref name="token" /> is cancelled.
    /// </summary>
    /// <returns>The result of <paramref name="task" />.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="token" /> is cancelled first.</exception>
    public static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken token)
    {
        await WaitAsync((Task)task, token).ConfigureAwait(false);

        return await task.ConfigureAwait(false);
    }

    /// <inheritdoc cref="WaitAsync{T}" />
    public static async Task WaitAsync(Task task, CancellationToken token)
    {
        if (!task.IsCompleted && token.CanBeCanceled)
        {
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task)
                {
                    _ = task.ContinueWith(
                        static t => t.Exception,
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);

                    throw new OperationCanceledException(token);
                }
            }
        }

        await task.ConfigureAwait(false);
    }
}
