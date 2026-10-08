# Design decisions

Every important decision is recorded as an ADR in
[`docs/adr`](https://github.com/fsoftt/Murmur/tree/main/docs/adr) (in Spanish), with its context,
the alternatives and the consequences. This is the summary.

## ADR-001 · Local-first, no mailbox on the server {#adr-001}
The source of truth is each device's database. A message is only delivered when **both
phones are online at the same time**. *Cost:* a worse experience than with a central mailbox,
softened by background delivery ([ADR-012](#adr-012)).

## ADR-002 · No TURN in the MVP {#adr-002}
Traffic is not relayed through a server when there is no direct route. *Cost:* some pairs will
never connect. This will be measured before it becomes permanent.

## ADR-003 · Abstract transport, WebRTC as the target {#adr-003}
The domain only sees `IPeerLink`. Production: WebRTC DataChannel via a binding of `stream-webrtc-android`.
**SIPSorcery ruled out** because of a usage restriction in its license that is incompatible with the AGPL.

## ADR-004 · Identity model {#adr-004}
Ed25519 for signing + a static X25519 key for Noise, bound together in a signed card. No phone number,
email or global username. Ready for several cards per user (multi-device).

## ADR-005 · Noise per connection instead of Double Ratchet {#adr-005}
Without an asynchronous mailbox, a fresh Noise_KK handshake per connection gives *forward secrecy*
without persistent ratchet state. Our own implementation on top of audited primitives, validated with
independent vectors and the audit's top priority.

## ADR-006 · Relay transport for development only {#adr-006}
To test on phones before WebRTC, Debug builds route the (already encrypted) channel through the
server's relay, with rate limiting. Release builds don't include it.

## ADR-007 · Per-pair, rotating rendezvous topics {#adr-007}
`HKDF(DH(statics), UTC day)`: the server sees no identities and cannot track a pair across days
from the topic alone.

## ADR-008 · .NET MAUI, Android first, Clean Architecture + MVVM {#adr-008}
No web version (weaker guarantees). ViewModels in plain .NET so they can be tested on any OS.

## ADR-009 · SQLCipher with the key in the secure store {#adr-009}
A random 256-bit key in the Keystore, raw-key mode. The app refuses to open an unencrypted database.

## ADR-010 · Licensing {#adr-010}
AGPL-3.0 for the code (derivatives, including a modified server offered as a service,
stay auditable), CC BY 4.0 for the specification, DCO for contributions.

## ADR-011 · Ordering by Lamport clocks {#adr-011}
Total order `(lamport, creation, id)`, identical on both phones and immune to clock skew.

## ADR-012 · Background delivery and "Always available" {#adr-012}
**A.** A WorkManager job (every 15 minutes when there is a network, plus one when leaving the app)
starts the client and drains the outbox: the sender no longer has to open the app. **B.** An optional
foreground service, with its persistent notification, keeps the phone reachable with the app closed
and comes back after a reboot. Notifications never show the message text and the server still
stores nothing. *Cost:* somewhat more battery with B; iOS will need a different solution.
