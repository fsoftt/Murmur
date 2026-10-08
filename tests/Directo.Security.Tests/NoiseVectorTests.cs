using System.Text.Json;
using Directo.Security.Noise;

namespace Directo.Security.Tests;

/// <summary>Byte-exact interoperability with an independent Noise implementation.</summary>
public class NoiseVectorTests
{
    public static TheoryData<string> Patterns => new() { "KK", "IK" };

    [Theory]
    [MemberData(nameof(Patterns))]
    public void Handshake_and_transport_match_reference_implementation(string patternName)
    {
        var vector = LoadVector(patternName);
        var pattern = patternName == "KK" ? HandshakePattern.KK : HandshakePattern.IK;
        Assert.Equal(vector.GetProperty("protocol_name").GetString(), pattern.ProtocolName);

        var prologue = Hex(vector, "prologue");
        var initStatic = Hex(vector, "init_static");
        var respStatic = Hex(vector, "resp_static");
        var initStaticPublic = Primitives.Curve25519.X25519PublicKey(initStatic);
        var respStaticPublic = Primitives.Curve25519.X25519PublicKey(respStatic);

        using var initiator = new HandshakeState(pattern, true, prologue, initStatic, respStaticPublic, Hex(vector, "init_ephemeral"));
        using var responder = new HandshakeState(
            pattern,
            false,
            prologue,
            respStatic,
            patternName == "KK" ? initStaticPublic : default,
            Hex(vector, "resp_ephemeral"));

        var messages = vector.GetProperty("messages").EnumerateArray().ToArray();

        var m1 = initiator.WriteMessage(Hex(messages[0], "payload"));
        Assert.Equal(Hex(messages[0], "ciphertext"), m1);
        Assert.Equal(Hex(messages[0], "payload"), responder.ReadMessage(m1));

        var m2 = responder.WriteMessage(Hex(messages[1], "payload"));
        Assert.Equal(Hex(messages[1], "ciphertext"), m2);
        Assert.Equal(Hex(messages[1], "payload"), initiator.ReadMessage(m2));

        Assert.True(initiator.IsCompleted);
        Assert.True(responder.IsCompleted);
        Assert.Equal(Hex(vector, "handshake_hash"), initiator.HandshakeHash);
        Assert.Equal(Hex(vector, "handshake_hash"), responder.HandshakeHash);
        Assert.Equal(initStaticPublic, responder.RemoteStaticKey);

        using var initiatorTransport = initiator.Split();
        using var responderTransport = responder.Split();
        var directions = new[] { (initiatorTransport, responderTransport), (responderTransport, initiatorTransport), (initiatorTransport, responderTransport) };
        for (var i = 0; i < directions.Length; i++)
        {
            var (sender, receiver) = directions[i];
            var expected = messages[2 + i];
            var ciphertext = sender.Encrypt(Hex(expected, "payload"));
            Assert.Equal(Hex(expected, "ciphertext"), ciphertext);
            Assert.Equal(Hex(expected, "payload"), receiver.Decrypt(ciphertext));
        }
    }

    private static JsonElement LoadVector(string pattern)
    {
        var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestVectors", "noise-vectors.json")));
        return json.RootElement.GetProperty("vectors").EnumerateArray()
            .Single(v => v.GetProperty("protocol_name").GetString()!.Contains($"_{pattern}_", StringComparison.Ordinal));
    }

    private static byte[] Hex(JsonElement element, string property) => Convert.FromHexString(element.GetProperty(property).GetString()!);
}
