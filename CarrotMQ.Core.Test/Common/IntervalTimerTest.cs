using CarrotMQ.Core.Common;

namespace CarrotMQ.Core.Test.Common;

[TestClass]
public class IntervalTimerTest
{
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
        Assert.IsTrue(
            elapsedTriggeredCounter > minimumExpectedAmountOfTriggers,
            $"IntervalTier should have triggered at least {minimumExpectedAmountOfTriggers} times, but it only did trigger {elapsedTriggeredCounter} times");
    }
}