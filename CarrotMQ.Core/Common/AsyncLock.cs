using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarrotMQ.Core.Common;

/// <summary>
/// Represents an asynchronous lock that can be used to synchronize access to a shared resource.
/// </summary>
public sealed class AsyncLock
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Asynchronously acquires the lock, returning a disposable object that releases the lock when disposed.
    /// </summary>
    /// <returns>An awaitable task that yields a disposable object representing the lock scope.</returns>
    public async Task<IDisposable> LockAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);

        return new Scope(_lock);
    }

    private readonly struct Scope : IDisposable
    {
        private readonly SemaphoreSlim _lock;

        internal Scope(SemaphoreSlim @lock)
        {
            _lock = @lock;
        }

        /// <summary>
        /// Releases the acquired lock when the disposable object is disposed.
        /// </summary>
        public void Dispose()
        {
            _lock?.Release();
        }
    }
}