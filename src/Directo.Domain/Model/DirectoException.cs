namespace Directo.Domain.Model;

/// <summary>Domain-level failure categories. The UI maps each to an understandable message.</summary>
public enum DirectoErrorCode
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

public sealed class DirectoException(DirectoErrorCode code, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public DirectoErrorCode Code { get; } = code;
}
