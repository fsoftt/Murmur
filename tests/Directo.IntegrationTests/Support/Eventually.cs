namespace Directo.IntegrationTests.Support;

public static class Eventually
{
    public static async Task TrueAsync(Func<Task<bool>> condition, string because, int timeoutMs = 15_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail($"Timed out waiting until {because}.");
    }

    public static Task TrueAsync(Func<bool> condition, string because, int timeoutMs = 15_000) =>
        TrueAsync(() => Task.FromResult(condition()), because, timeoutMs);
}
