using CarrotMQ.Core.Common;

#pragma warning disable MA0147

namespace CarrotMQ.Core.Test.Common;

[TestClass]
public class AsyncLockTests
{
    [TestMethod]
    public async Task AsyncLock_AcquiresAndReleasesLock()
    {
        // Arrange
        var asyncLock = new AsyncLock();

        // Act
        using (await asyncLock.LockAsync().ConfigureAwait(false))
        {
            // Assert
            Assert.IsTrue(true, "Lock acquired successfully.");
        }
    }

#if NET8_0_OR_GREATER
    [TestMethod]
    public async Task AsyncLock_AcquiresAndReleasesLock_Parallel()
    {
        // Arrange
        var asyncLock = new AsyncLock();

        var lockCounter = 0;
        await Parallel.ForAsync(
            0,
            100,
            async (_, token) =>
            {
                // Act

                using (await asyncLock.LockAsync().ConfigureAwait(false))
                {
                    Interlocked.Increment(ref lockCounter);
                    // Assert
                    Assert.IsTrue(true, "Lock acquired successfully.");
                    await Task.Delay(10, token).ConfigureAwait(false);
                    Assert.AreEqual(1, lockCounter, "More than one threads could access the lock at once.");
                    Interlocked.Decrement(ref lockCounter);
                }
            });
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
        Parallel.For(
            0,
            100,
            // ReSharper disable once AsyncVoidLambda
            async _ =>
            {
                // Act
                using (await asyncLock.LockAsync().ConfigureAwait(false))
                {
                    Interlocked.Increment(ref lockCounter);
                    // Assert
                    Assert.IsTrue(true, "Lock acquired successfully.");
                    await Task.Delay(10).ConfigureAwait(false);
                    Assert.AreEqual(1, lockCounter, "More than one threads could access the lock at once.");
                    Interlocked.Decrement(ref lockCounter);
                }

                Interlocked.Increment(ref lockCompletedCounter);
                if (lockCompletedCounter >= 100)
                {
                    allLockCompletedTcs.SetResult(true);
                }
            });

        await allLockCompletedTcs.Task;
    }
#endif
}