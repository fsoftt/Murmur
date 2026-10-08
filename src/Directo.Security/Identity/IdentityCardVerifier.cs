using Directo.Protocol.Identity;
using Directo.Security.Primitives;

namespace Directo.Security.Identity;

public static class IdentityCardVerifier
{
    public static bool IsValid(IdentityCard card) =>
        card.IdentityKey.Length == Curve25519.KeySize
        && card.StaticKey.Length == Curve25519.KeySize
        && Curve25519.Ed25519Verify(card.IdentityKey, card.ToBeSigned(), card.Signature);

    public static void Verify(IdentityCard card)
    {
        if (!IsValid(card))
        {
            throw new CryptoException("Identity card signature is invalid.");
        }
    }
}
