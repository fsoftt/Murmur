# Privacy and threats

> Summary of the [full threat model](https://github.com/fsoftt/Murmur/blob/main/docs/threat-model.md) (in Spanish).
> **End-to-end encryption protects the content; it does not remove all metadata.**

## Who sees what

| | Content | Contacts | Who talks to whom | Your IP | When you're online |
|---|---|---|---|---|---|
| **Signaling server** | ❌ | ❌ | Only "these two connections share a random topic today" | ✅ | ✅ |
| **Hostile network or Wi-Fi** | ❌ (encrypted and authenticated) | ❌ | Sees traffic between two IPs | ✅ | ✅ |
| **Your contact** | ✅ | — | — | ✅ (P2P) | ✅ |
| **Thief with your locked phone** | ❌ (SQLCipher + Keystore) | ❌ | ❌ | — | — |
| **Malware on your unlocked phone** | ✅ | ✅ | ✅ | ✅ | ✅ |

The last row is the honest one: **end-to-end security ends at the ends.**

## What the server never receives

Message text · names · identity keys · contact lists · invites · private keys.
An integration test records **all** server traffic during a pairing and a message exchange,
and checks that none of those appear.

## Cryptographic properties

- **Confidentiality and integrity** of every message (ChaCha20-Poly1305).
- **Mutual authentication** in every session (Noise_KK with the keys from pairing).
- **Per-session forward secrecy**: recording traffic today and stealing the phone tomorrow gets you nothing.
- **Replay and reordering detection** (counter nonces).
- No *post-compromise security* within a single session ([ADR-005](/guide/decisions#adr-005)).

## Hygiene

- Messages, keys, tokens, invites, topics and IPs are never logged.
- No analytics, advertising or crash-reporting SDKs.
- Android backups are disabled.
- Error messages use plain language, never stack traces.

## Audit priorities

1. The Noise implementation.
2. Pairing and invite verification.
3. Network parsers and limits.
4. Local storage and key management.
5. The signaling server (abuse and metadata).
