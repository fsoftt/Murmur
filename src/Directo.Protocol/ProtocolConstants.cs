namespace Directo.Protocol;

/// <summary>
/// Versioning and hard limits of the Directo wire protocol. Limits are enforced before
/// any parsing so that hostile input cannot force large allocations.
/// </summary>
public static class ProtocolConstants
{
    /// <summary>Highest protocol version this implementation speaks.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Lowest protocol version this implementation still accepts.</summary>
    public const int MinSupportedVersion = 1;

    public const int PublicKeySize = 32;
    public const int SignatureSize = 64;
    public const int MessageIdSize = 16;
    public const int PairingTokenSize = 16;

    /// <summary>Maximum UTF-8 size of a text message body.</summary>
    public const int MaxMessageBodyBytes = 16 * 1024;

    /// <summary>Maximum UTF-8 size of a self-chosen profile name.</summary>
    public const int MaxProfileNameBytes = 64;

    /// <summary>Maximum plaintext size of a single peer frame (fits in one Noise message).</summary>
    public const int MaxFrameBytes = 60 * 1024;

    /// <summary>Maximum size of a decoded invite.</summary>
    public const int MaxInviteBytes = 1024;

    /// <summary>Maximum number of capability strings a peer may announce.</summary>
    public const int MaxCapabilities = 32;

    /// <summary>Maximum length of a single capability string.</summary>
    public const int MaxCapabilityLength = 64;
}
