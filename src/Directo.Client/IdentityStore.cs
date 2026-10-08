using System.Security.Cryptography;
using Directo.Domain.Ports;
using Directo.Security.Identity;

namespace Directo.Client;

/// <summary>Creates the device identity on first run and keeps its secret keys in the platform secret store.</summary>
public static class IdentityStore
{
    public const string SecretName = "directo.identity.v1";

    public static async Task<LocalIdentityKeys> LoadOrCreateAsync(ISecretStore secrets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        var stored = await secrets.GetAsync(SecretName, cancellationToken).ConfigureAwait(false);
        if (stored is not null)
        {
            try
            {
                return LocalIdentityKeys.Deserialize(stored);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(stored);
            }
        }

        var keys = LocalIdentityKeys.Generate();
        var serialized = keys.Serialize();
        try
        {
            await secrets.SetAsync(SecretName, serialized, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(serialized);
        }

        return keys;
    }
}
