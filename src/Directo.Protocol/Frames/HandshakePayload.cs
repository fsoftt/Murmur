using System.Formats.Cbor;
using Directo.Protocol.Serialization;
using Directo.Protocol.Identity;

namespace Directo.Protocol.Frames;

/// <summary>
/// Payload carried (encrypted) inside the Noise handshake messages. Every handshake announces
/// the supported protocol range; pairing handshakes additionally carry the initiator's identity
/// card and the single-use invite token.
/// </summary>
public sealed record HandshakePayload(
    int MinVersion,
    int MaxVersion,
    IReadOnlyList<string> Capabilities,
    IdentityCard? Card = null,
    byte[]? PairingToken = null,
    string? ProfileName = null)
{
    public static HandshakePayload ForCurrentVersion(IdentityCard? card = null, byte[]? pairingToken = null, string? profileName = null) =>
        new(ProtocolConstants.MinSupportedVersion, ProtocolConstants.CurrentVersion, [], card, pairingToken, profileName);

    /// <summary>Picks the highest version both sides support.</summary>
    public static int Negotiate(HandshakePayload local, HandshakePayload remote)
    {
        var version = Math.Min(local.MaxVersion, remote.MaxVersion);
        if (version < Math.Max(local.MinVersion, remote.MinVersion))
        {
            throw new ProtocolException(
                ProtocolErrorCode.UnsupportedVersion,
                $"No common protocol version (local {local.MinVersion}-{local.MaxVersion}, remote {remote.MinVersion}-{remote.MaxVersion}).");
        }

        return version;
    }
}

public static class HandshakePayloadCodec
{
    private const uint KeyMinVersion = 0;
    private const uint KeyMaxVersion = 1;
    private const uint KeyCapabilities = 2;
    private const uint KeyCard = 3;
    private const uint KeyPairingToken = 4;
    private const uint KeyProfileName = 5;

    public const int MaxBytes = 2048;

    public static byte[] Encode(HandshakePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var writer = CborMap.CreateWriter();
        var fields = 3 + (payload.Card is null ? 0 : 1) + (payload.PairingToken is null ? 0 : 1) + (payload.ProfileName is null ? 0 : 1);
        writer.WriteStartMap(fields);
        writer.WriteUInt32(KeyMinVersion);
        writer.WriteInt32(payload.MinVersion);
        writer.WriteUInt32(KeyMaxVersion);
        writer.WriteInt32(payload.MaxVersion);
        writer.WriteUInt32(KeyCapabilities);
        writer.WriteStartArray(payload.Capabilities.Count);
        foreach (var capability in payload.Capabilities)
        {
            writer.WriteTextString(capability);
        }

        writer.WriteEndArray();
        if (payload.Card is not null)
        {
            writer.WriteUInt32(KeyCard);
            writer.WriteByteString(IdentityCardCodec.Encode(payload.Card));
        }

        if (payload.PairingToken is not null)
        {
            writer.WriteUInt32(KeyPairingToken);
            writer.WriteByteString(payload.PairingToken);
        }

        if (payload.ProfileName is not null)
        {
            writer.WriteUInt32(KeyProfileName);
            writer.WriteTextString(payload.ProfileName);
        }

        writer.WriteEndMap();
        return writer.Encode();
    }

    public static HandshakePayload Decode(ReadOnlyMemory<byte> data)
    {
        var reader = CborMap.CreateReader(data, MaxBytes);
        int? min = null;
        int? max = null;
        var capabilities = new List<string>();
        IdentityCard? card = null;
        byte[]? token = null;
        string? profileName = null;

        CborMap.ReadMap(reader, (key, r) =>
        {
            switch (key)
            {
                case KeyMinVersion:
                    min = r.ReadInt32();
                    return true;
                case KeyMaxVersion:
                    max = r.ReadInt32();
                    return true;
                case KeyCapabilities:
                    var count = r.ReadStartArray();
                    if (count is null || count > ProtocolConstants.MaxCapabilities)
                    {
                        throw ProtocolException.Malformed("Too many capabilities.");
                    }

                    for (var i = 0; i < count; i++)
                    {
                        capabilities.Add(CborMap.ReadBoundedText(r, ProtocolConstants.MaxCapabilityLength, "capability"));
                    }

                    r.ReadEndArray();
                    return true;
                case KeyCard:
                    card = IdentityCardCodec.Decode(r.ReadByteString());
                    return true;
                case KeyPairingToken:
                    token = CborMap.ReadFixedBytes(r, ProtocolConstants.PairingTokenSize, "pairingToken");
                    return true;
                case KeyProfileName:
                    profileName = CborMap.ReadBoundedText(r, ProtocolConstants.MaxProfileNameBytes, "profileName");
                    return true;
                default:
                    return false;
            }
        });
        CborMap.EnsureFullyConsumed(reader);

        var minVersion = CborMap.Required(min, "minVersion");
        var maxVersion = CborMap.Required(max, "maxVersion");
        if (minVersion < 1 || maxVersion < minVersion)
        {
            throw ProtocolException.Malformed("Invalid version range.");
        }

        return new HandshakePayload(minVersion, maxVersion, capabilities, card, token, profileName);
    }
}
