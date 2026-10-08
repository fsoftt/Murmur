using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Murmur.Client;

namespace Murmur.Presentation.ViewModels;

/// <summary>Local profile. The name is only shared with people who pair with this device.</summary>
public sealed partial class SettingsViewModel(MurmurClient client) : ViewModelBase
{
    [ObservableProperty]
    public partial string ProfileName { get; set; } = string.Empty;

    /// <summary>Short fingerprint of this device's identity key, for diagnostics.</summary>
    [ObservableProperty]
    public partial string IdentityFingerprint { get; set; } = string.Empty;

    public Task LoadAsync() => RunAsync(async () =>
    {
        ProfileName = await client.GetProfileNameAsync() ?? string.Empty;
        IdentityFingerprint = Convert.ToHexString(client.IdentityKey.Span[..8]);
    });

    [RelayCommand]
    private Task SaveAsync() => RunAsync(() => client.SetProfileNameAsync(ProfileName));
}
