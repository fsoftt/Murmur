namespace Murmur.Core.Tests.Support;

public static class Eventually
{
    public static async Task TrueAsync(Func<Task<bool>> condition, string because, int timeoutMs = 10_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail($"Timed out waiting until {because}.");
    }
}
