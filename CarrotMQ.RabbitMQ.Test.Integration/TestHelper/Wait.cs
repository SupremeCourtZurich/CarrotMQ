namespace CarrotMQ.RabbitMQ.Test.Integration.TestHelper;

internal static class Wait
{
    /// <summary>
    /// Completes once <paramref name="condition" /> holds; throws <see cref="TimeoutException" /> if it does not within 20 s.
    /// </summary>
    public static async Task UntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"Not within 20 s: {description}");

            await Task.Delay(10).ConfigureAwait(false);
        }
    }
}