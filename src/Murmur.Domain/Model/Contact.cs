namespace Murmur.Domain.Model;

public enum VerificationState
{
    /// <summary>Paired, but the users have not compared safety numbers.</summary>
    Unverified = 0,

    /// <summary>The user confirmed that both devices show the same safety number.</summary>
    Verified = 1,
}

/// <summary>A paired peer. Contacts exist only on this device; the server has no address book.</summary>
public sealed record Contact(
    ContactId Id,
    PeerIdentity Identity,
    string DisplayName,
    VerificationState Verification,
    bool IsBlocked,
    DateTimeOffset CreatedAt)
{
    public const int MaxDisplayNameLength = 64;

    public static string NormalizeDisplayName(string? name, string fallback)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return fallback;
        }

        return trimmed.Length <= MaxDisplayNameLength ? trimmed : trimmed[..MaxDisplayNameLength];
    }
}
