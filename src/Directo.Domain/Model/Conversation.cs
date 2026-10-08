namespace Directo.Domain.Model;

/// <summary>One-to-one conversation. <paramref name="LamportClock"/> is the highest logical time seen.</summary>
public sealed record Conversation(ConversationId Id, ContactId ContactId, long LamportClock, DateTimeOffset CreatedAt);

/// <summary>Read model for the conversation list.</summary>
public sealed record ConversationSummary(
    ConversationId Id,
    ContactId ContactId,
    string DisplayName,
    string? LastMessagePreview,
    DateTimeOffset? LastActivityAt,
    int PendingCount);
