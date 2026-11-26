namespace CarrotMQ.RabbitMQ.Test.Helper;

internal static class Wait
{
    /// <summary>
    /// Completes once <paramref name="condition" /> holds; throws <see cref="TimeoutException" /> if it does not within 4 s.
    /// </summary>
    public static async Task UntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(4);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"Not within 4 s: {description}");

            await Task.Delay(10).ConfigureAwait(false);
        }
    }
}
