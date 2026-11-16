using CarrotMQ.Core.Common;

namespace CarrotMQ.Core.Test.Common;

[TestClass]
public class IntervalTimerTest
{
    // Without the guard the timer restart after the handler throws in an async void method and kills the test host.
    [TestMethod]
    [Timeout(5_000)]
    public async Task DisposedWhileTheElapsedHandlerRuns_HandlerFinishesWithoutError()
    {
        // Arrange
        var handlerEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource<bool>();
        var handlerFinished = false;
        var timer = new IntervalTimer(1);
        timer.ElapsedAsync += async (_, _) =>
        {
            handlerEntered.TrySetResult(true);
            await releaseHandler.Task.ConfigureAwait(false);
            handlerFinished = true;
        };
        timer.Start();
        await handlerEntered.Task.ConfigureAwait(false);

        // Act
        timer.Dispose();
        releaseHandler.SetResult(true);

        // Assert
        Assert.IsTrue(handlerFinished);
    }

    [TestMethod]
    public async Task TimerNotStarted_ShouldNotTrigger_Test()
    {
        const int timerIntervalMs = 10;
        const int amountOfIntervals = 10;
        var elapsedTriggeredCounter = 0;
        using IntervalTimer timer = new(timerIntervalMs);
        timer.ElapsedAsync += (_, _) =>
        {
            elapsedTriggeredCounter++;

            return Task.CompletedTask;
        };

        await Task.Delay(amountOfIntervals * timerIntervalMs);

        Assert.AreEqual(0, elapsedTriggeredCounter);
    }

    [TestMethod]
    public async Task TimerStopped_ShouldNotTrigger_Test()
    {
        const int timerIntervalMs = 10;
        const int amountOfIntervals = 10;
        var elapsedTriggeredCounter = 0;
        using IntervalTimer timer = new(timerIntervalMs);
        timer.ElapsedAsync += (_, _) =>
        {
            elapsedTriggeredCounter++;

            return Task.CompletedTask;
        };
        timer.Start();
        timer.Stop();

        await Task.Delay(amountOfIntervals * timerIntervalMs);

        Assert.AreEqual(0, elapsedTriggeredCounter);
    }

    [TestMethod]
    public async Task TimerStarted_ShouldTrigger_Test()
    {
        const int timerIntervalMs = 100;
        const int amountOfIntervals = 10;
        var elapsedTriggeredCounter = 0;
        using IntervalTimer timer = new(timerIntervalMs);
        timer.ElapsedAsync += (_, _) =>
        {
            elapsedTriggeredCounter++;
            Console.WriteLine($"Trigger {elapsedTriggeredCounter}");

            return Task.CompletedTask;
        };
        timer.Start();

        await Task.Delay(amountOfIntervals * timerIntervalMs);

        var minimumExpectedAmountOfTriggers =
            (int)(amountOfIntervals * 0.5); // only expect 50% of the triggers to actually hit, due to very small interval of 10ms
        Assert.IsGreaterThan(
            minimumExpectedAmountOfTriggers,
            elapsedTriggeredCounter,
            $"IntervalTier should have triggered at least {minimumExpectedAmountOfTriggers} times, but it only did trigger {elapsedTriggeredCounter} times");
    }
}