using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Murmur.Client;
using Murmur.Presentation.Services;

namespace Murmur.Presentation.ViewModels;

/// <summary>Local profile and background availability. The name is only shared with people who pair with this device.</summary>
public sealed partial class SettingsViewModel(MurmurClient client, IAvailabilityService availability) : ViewModelBase
{
    [ObservableProperty]
    public partial string ProfileName { get; set; } = string.Empty;

    /// <summary>Short fingerprint of this device's identity key, for diagnostics.</summary>
    [ObservableProperty]
    public partial string IdentityFingerprint { get; set; } = string.Empty;

    public bool AvailabilitySupported => availability.IsSupported;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBatteryWarning))]
    public partial bool AlwaysAvailable { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBatteryWarning))]
    public partial bool BatteryOptimized { get; set; }

    /// <summary>The mode is on, but the OS may still pause the app to save battery.</summary>
    public bool ShowBatteryWarning => AlwaysAvailable && BatteryOptimized;

    public Task LoadAsync() => RunAsync(async () =>
    {
        ProfileName = await client.GetProfileNameAsync() ?? string.Empty;
        IdentityFingerprint = Convert.ToHexString(client.IdentityKey.Span[..8]);
        RefreshAvailability();
    });

    /// <summary>Re-reads the platform state (e.g. after returning from the battery settings).</summary>
    public void RefreshAvailability()
    {
        AlwaysAvailable = availability.IsAlwaysAvailable;
        BatteryOptimized = availability.IsBatteryOptimized;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(() => client.SetProfileNameAsync(ProfileName));

    [RelayCommand]
    private Task SetAlwaysAvailableAsync(bool enabled) => RunAsync(async () =>
    {
        AlwaysAvailable = await availability.SetAlwaysAvailableAsync(enabled);
        BatteryOptimized = availability.IsBatteryOptimized;
        if (enabled && !AlwaysAvailable)
        {
            ErrorMessage = "Sin permiso de notificaciones no se puede mantener Murmur disponible en segundo plano.";
        }
    });

    [RelayCommand]
    private void OpenBatterySettings() => availability.OpenBatterySettings();
}
