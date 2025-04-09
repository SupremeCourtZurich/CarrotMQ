using CarrotMQ.Core.Common;

namespace CarrotMQ.Core.Test.Common;

[TestClass]
public class AsyncLockTests
{
#if NET
    [TestMethod]
    public async Task AsyncLock_AcquiresAndReleasesLock_Parallel()
    {
        // Arrange
        var asyncLock = new AsyncLock();

        var lockCounter = 0;
        await Parallel.ForAsync(
            0,
            100,
            async (x, token) =>
            {
                // Act

                using (await asyncLock.LockAsync().ConfigureAwait(false))
                {
                    _ = Interlocked.Increment(ref lockCounter);
                    await Task.Delay(10, token).ConfigureAwait(false);
                    _ = Interlocked.CompareExchange(ref lockCounter, 0, 1); // Set back to 0 only if lockCounter equals 1
                }
            });

        Assert.AreEqual(0, lockCounter, nameof(lockCounter));
    }
#else
    [TestMethod]
    public async Task AsyncLock_AcquiresAndReleasesLock_Parallel()
    {
        // Arrange
        var asyncLock = new AsyncLock();

        var lockCounter = 0;

        var lockCompletedCounter = 0;

        TaskCompletionSource<bool> allLockCompletedTcs = new();
        _ = Parallel.For(
            0,
            100,
            // ReSharper disable once AsyncVoidLambda
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable MA0147
            async x =>
            {
                // Act
                using (await asyncLock.LockAsync().ConfigureAwait(false))
                {
                    _ = Interlocked.Increment(ref lockCounter);
                    await Task.Delay(10).ConfigureAwait(false);
                    _ = Interlocked.CompareExchange(ref lockCounter, 0, 1); // Set back to 0 only if lockCounter equals 1
                }

                _ = Interlocked.Increment(ref lockCompletedCounter);
                if (lockCompletedCounter >= 100)
                {
                    allLockCompletedTcs.SetResult(true);
                }
            });

        _ = await allLockCompletedTcs.Task;
        Assert.AreEqual(0, lockCounter, nameof(lockCounter));
    }
#endif
}