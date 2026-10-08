namespace Directo.Signaling.Server;

/// <summary>Abuse limits. Defaults are deliberately conservative for a small deployment.</summary>
public sealed class SignalingOptions
{
    public const string SectionName = "Signaling";

    public int MaxTopicsPerConnection { get; set; } = 512;

    /// <summary>Contact topics have two members; a few more leaves room for multi-device later.</summary>
    public int MaxSubscribersPerTopic { get; set; } = 8;

    public int MaxConnectionsPerAddress { get; set; } = 32;

    public int MaxConnections { get; set; } = 50_000;

    /// <summary>Sustained messages per second per connection (token bucket refill rate).</summary>
    public double MessagesPerSecond { get; set; } = 20;

    /// <summary>Token bucket size per connection.</summary>
    public int MessageBurst { get; set; } = 100;

    /// <summary>Messages queued towards a slow client before it is disconnected.</summary>
    public int MaxOutboundQueue { get; set; } = 256;

    public TimeSpan HelloTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan KeepAliveTimeout { get; set; } = TimeSpan.FromSeconds(60);
}
