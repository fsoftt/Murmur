# How it was built

The project started from an architecture document written before a single line of code
([original, in Spanish](https://github.com/fsoftt/Murmur/blob/main/docs/history/arquitectura-inicial.md)).
It was built in small, verifiable steps, following one rule:

> **Every layer is tested before the next one is built on top of it.**

## 1. Review the design before writing code

Reviewing the initial document changed several decisions:

- **Noise instead of Double Ratchet.** Double Ratchet exists for asynchronous messaging with a mailbox.
  Murmur only delivers when both sides are online, so a fresh handshake per connection gives *forward
  secrecy* with far less complexity ([ADR-005](/guide/decisions#adr-005)).
- **Two-way pairing.** The initial flow didn't explain how the inviter learns about the person who
  scans; this was solved with Noise_IK.
- **Private presence from the MVP**, not "in the future": per-pair, rotating topics.
- **Explicit product decisions**: no mailbox (delivery happens when both phones are online at the same time), IP visible to contacts, no recovery.

## 2. Protocol and cryptography first

1. CBOR wire formats, with limits checked before parsing and unknown keys ignored.
2. Noise transcribed from the specification on top of BouncyCastle primitives.
3. **Test vectors generated with an independent implementation** (Python `noiseprotocol`):
   our implementation produces **the same bytes** for every handshake and transport message.
4. Fuzzing: 20,000 random or bit-flipped inputs can produce nothing but a controlled
   protocol error.

## 3. The domain, without infrastructure

Entities, use cases and the delivery engine were written against interfaces. The engine was tested
with an in-memory channel with fault injection (lost ACKs, disconnects, restarts) and a real
SQLCipher database.

## 4. Encrypted storage

SQLCipher with a raw 256-bit key from the secure store. A test opens the database file
and checks that it contains neither the message text nor the SQLite header.

## 5. Network, server and pairing

An ASP.NET Core server with anti-abuse limits, a client with reconnection and resubscription, Noise
sessions over a transport abstraction, and the connection manager with its state machine.
The integration tests spin up **the real server in-process and two complete devices**.

## 6. Presentation and app

ViewModels in plain .NET (testable on Linux) with MAUI views on top. The app also builds for
`net10.0`, which makes it possible to check the XAML without the Android SDK.

## Findings along the way

- **SIPSorcery ruled out.** The best-known C# WebRTC library added a geopolitical usage
  restriction to its BSD license. That takes it out of open source and makes it incompatible with
  the AGPL, so phase 4 will use a binding of `stream-webrtc-android` (Apache-2.0) ([ADR-003](/guide/decisions#adr-003)).
- **A flaky test** uncovered a race in the tests' simulated network (not in the product):
  simulating "unreachable" didn't affect attempts already in progress.
- **The development relay fell out of sync** under many messages: the server dropped frames
  because of its rate limit, and Noise, with counter nonces, doesn't tolerate gaps. A client-side
  limiter was added; the 80-message test fails without it and passes with it.
- **`Base64Url.TryDecodeFromChars` throws** on non-canonical input instead of returning
  `false`: a topic-validation test caught it.
