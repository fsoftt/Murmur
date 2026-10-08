using Android.Content;
using AndroidX.Work;
using Murmur.App.Services;

namespace Murmur.App.Platforms.Android.Background;

/// <summary>
/// Option A: a WorkManager job that runs even when the app was never opened since boot. It
/// starts the client, tries to deliver every pending message for a few minutes, and stops.
/// It only succeeds while the recipient is reachable (online or "always available").
/// </summary>
public sealed class OutboxWorker(Context context, WorkerParameters parameters) : Worker(context, parameters)
{
    private static readonly TimeSpan Budget = TimeSpan.FromMinutes(8);

    public override Result DoWork()
    {
        if (IPlatformApplication.Current?.Services.GetService(typeof(ClientHost)) is not ClientHost host)
        {
            return Result.InvokeRetry();
        }

        try
        {
            host.InitializeAsync().GetAwaiter().GetResult();
            host.Client.DeliverPendingAsync(Budget).GetAwaiter().GetResult();

            // Undelivered messages are retried by the periodic schedule; no need to fail the job.
            return Result.InvokeSuccess();
        }
        catch (Exception)
        {
            return Result.InvokeRetry();
        }
    }
}

internal static class BackgroundDelivery
{
    private const string PeriodicName = "murmur-outbox-periodic";
    private const string OneShotName = "murmur-outbox-now";

    /// <summary>Every 15 minutes (Android's minimum) while there is a network, plus one run right away.</summary>
    public static void Schedule(Context context)
    {
        var constraints = new Constraints.Builder()
            .SetRequiredNetworkType(NetworkType.Connected!)
            .Build();
        var workerClass = Java.Lang.Class.FromType(typeof(OutboxWorker));

        var periodicBuilder = new PeriodicWorkRequest.Builder(workerClass, 15, Java.Util.Concurrent.TimeUnit.Minutes!);
        periodicBuilder.SetConstraints(constraints);
        var periodic = (PeriodicWorkRequest)periodicBuilder.Build();

        var nowBuilder = new OneTimeWorkRequest.Builder(workerClass);
        nowBuilder.SetConstraints(constraints);
        var now = (OneTimeWorkRequest)nowBuilder.Build();

        var workManager = WorkManager.GetInstance(context);
        workManager.EnqueueUniquePeriodicWork(PeriodicName, ExistingPeriodicWorkPolicy.Keep!, periodic);
        workManager.EnqueueUniqueWork(OneShotName, ExistingWorkPolicy.Replace!, now);
    }
}
