namespace Murmur.Security.Identity;

public static class KeyOrdering
{
    /// <summary>
    /// Deterministic role assignment so that two peers that see each other at the same time do
    /// not both start a session: the device with the lexicographically smaller static key initiates.
    /// </summary>
    public static bool IsInitiator(ReadOnlySpan<byte> localStaticKey, ReadOnlySpan<byte> remoteStaticKey) =>
        localStaticKey.SequenceCompareTo(remoteStaticKey) < 0;
}
