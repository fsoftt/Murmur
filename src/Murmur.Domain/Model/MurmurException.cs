namespace Murmur.Domain.Model;

/// <summary>Domain-level failure categories. The UI maps each to an understandable message.</summary>
public enum MurmurErrorCode
{
    ContactNotFound,
    ContactBlocked,
    ConversationNotFound,
    InvalidMessage,
    MessageTooLarge,
    PeerUnavailable,
    PeerConnectionFailed,
    IdentityVerificationFailed,
    MessageAuthenticationFailed,
    ProtocolVersionUnsupported,
    InviteInvalid,
    InviteExpired,
    AlreadyPaired,
    IdentityMissing,
    StorageFailure,
}

public sealed class MurmurException(MurmurErrorCode code, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public MurmurErrorCode Code { get; } = code;
}
