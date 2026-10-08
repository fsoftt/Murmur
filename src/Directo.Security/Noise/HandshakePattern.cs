namespace Directo.Security.Noise;

internal enum Token
{
    E,
    S,
    EE,
    ES,
    SE,
    SS,
}

/// <summary>The two interactive patterns Directo uses (spec §7.4).</summary>
public sealed class HandshakePattern
{
    /// <summary>Both sides already know each other's static key (regular contact sessions).</summary>
    public static readonly HandshakePattern KK = new(
        "KK",
        initiatorPreMessage: true,
        responderPreMessage: true,
        [[Token.E, Token.ES, Token.SS], [Token.E, Token.EE, Token.SE]]);

    /// <summary>Initiator knows the responder's static key and transmits its own (pairing via QR).</summary>
    public static readonly HandshakePattern IK = new(
        "IK",
        initiatorPreMessage: false,
        responderPreMessage: true,
        [[Token.E, Token.ES, Token.S, Token.SS], [Token.E, Token.EE, Token.SE]]);

    private HandshakePattern(string name, bool initiatorPreMessage, bool responderPreMessage, Token[][] messages)
    {
        Name = name;
        InitiatorPreMessage = initiatorPreMessage;
        ResponderPreMessage = responderPreMessage;
        Messages = messages;
    }

    public string Name { get; }

    internal bool InitiatorPreMessage { get; }

    internal bool ResponderPreMessage { get; }

    internal Token[][] Messages { get; }

    public string ProtocolName => $"Noise_{Name}_25519_ChaChaPoly_SHA256";
}
