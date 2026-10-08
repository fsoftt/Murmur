namespace Directo.Protocol.Frames;

/// <summary>Application frame exchanged inside an established secure session.</summary>
public abstract record PeerFrame;

/// <summary>A text message. <paramref name="Lamport"/> orders messages independently of wall clocks.</summary>
public sealed record ChatMessageFrame(Guid MessageId, long Lamport, long SentAtUnixMs, string Body) : PeerFrame;

/// <summary>Confirms that the receiver durably stored the message with <paramref name="MessageId"/>.</summary>
public sealed record AckFrame(Guid MessageId) : PeerFrame;

/// <summary>A frame type introduced by a newer protocol revision. Receivers ignore it.</summary>
public sealed record UnknownFrame(uint Type) : PeerFrame;
