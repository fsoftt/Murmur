namespace Murmur.Domain.Model;

public readonly record struct ContactId(Guid Value)
{
    public static ContactId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString("N");
}

public readonly record struct ConversationId(Guid Value)
{
    public static ConversationId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString("N");
}

/// <summary>Globally unique message id chosen by the sender; the basis of idempotent delivery.</summary>
public readonly record struct MessageId(Guid Value)
{
    public static MessageId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString("N");
}
