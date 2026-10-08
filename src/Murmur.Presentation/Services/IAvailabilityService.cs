namespace Murmur.Presentation.Services;

/// <summary>
/// Platform support for delivering without the app open: periodic background delivery
/// (option A) and an opt-in "always available" mode that keeps the phone reachable (option B).
/// </summary>
public interface IAvailabilityService
{
    /// <summary>False where the platform cannot keep the app reachable in the background.</summary>
    bool IsSupported { get; }

    bool IsAlwaysAvailable { get; }

    /// <summary>True when the OS may pause the app to save battery even in "always available" mode.</summary>
    bool IsBatteryOptimized { get; }

    /// <summary>Turns the mode on or off. Returns the resulting state (false if the user denied notifications).</summary>
    Task<bool> SetAlwaysAvailableAsync(bool enabled);

    /// <summary>Makes sure the periodic background delivery job is scheduled, and runs it soon. Safe from the background.</summary>
    void ScheduleBackgroundDelivery();

    /// <summary>Restarts "always available" if enabled. Only call while the app is visible (OS rule).</summary>
    void ResumeIfEnabled();

    void OpenBatterySettings();
}

/// <summary>Used on platforms without background support yet.</summary>
public sealed class UnsupportedAvailabilityService : IAvailabilityService
{
    public bool IsSupported => false;

    public bool IsAlwaysAvailable => false;

    public bool IsBatteryOptimized => false;

    public Task<bool> SetAlwaysAvailableAsync(bool enabled) => Task.FromResult(false);

    public void ScheduleBackgroundDelivery()
    {
    }

    public void ResumeIfEnabled()
    {
    }

    public void OpenBatterySettings()
    {
    }
}
