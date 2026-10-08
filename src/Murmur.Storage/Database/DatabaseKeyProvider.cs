using System.Security.Cryptography;
using Murmur.Domain.Ports;

namespace Murmur.Storage.Database;

/// <summary>Creates the random database key on first run and keeps it in the platform secret store, never next to the database.</summary>
public static class DatabaseKeyProvider
{
    public const string SecretName = "murmur.db-key.v1";

    public static async Task<byte[]> GetOrCreateAsync(ISecretStore secrets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        var existing = await secrets.GetAsync(SecretName, cancellationToken).ConfigureAwait(false);
        if (existing is { Length: 32 })
        {
            return existing;
        }

        var key = RandomNumberGenerator.GetBytes(32);
        await secrets.SetAsync(SecretName, key, cancellationToken).ConfigureAwait(false);
        return key;
    }
}
