using Directo.Domain.Ports;

namespace Directo.App.Services;

/// <summary>
/// Platform secure storage: on Android, values are encrypted with a key held in the Android
/// Keystore; on iOS, they live in the Keychain. Backups are disabled in the manifest.
/// </summary>
public sealed class MauiSecretStore : ISecretStore
{
    public async Task<byte[]?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        var value = await SecureStorage.Default.GetAsync(name);
        return value is null ? null : Convert.FromBase64String(value);
    }

    public Task SetAsync(string name, byte[] value, CancellationToken cancellationToken = default) =>
        SecureStorage.Default.SetAsync(name, Convert.ToBase64String(value));

    public Task RemoveAsync(string name, CancellationToken cancellationToken = default)
    {
        SecureStorage.Default.Remove(name);
        return Task.CompletedTask;
    }
}
