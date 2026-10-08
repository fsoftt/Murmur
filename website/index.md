---
layout: home

hero:
  name: Murmur
  text: Private messaging that doesn't need a server to store your conversations
  tagline: No accounts, no phone number, no email. You add contacts in person with a QR code, messages travel encrypted from phone to phone, and your history lives only on your devices.
  image:
    src: /favicon.svg
    alt: Murmur
  actions:
    - theme: brand
      text: What is Murmur
      link: /guide/overview
    - theme: alt
      text: Architecture
      link: /guide/architecture
    - theme: alt
      text: View the code on GitHub
      link: https://github.com/fsoftt/Murmur

features:
  - icon: 📱
    title: Local-first
    details: Your history and pending messages live in an encrypted database on your phone. No server keeps a copy.
    link: /guide/life-of-a-message
  - icon: 🔐
    title: End-to-end encrypted with Noise
    details: Every connection between two phones opens a fresh Noise_KK session with forward secrecy. Validated byte for byte against an independent implementation.
    link: /concepts/noise
  - icon: 🤝
    title: QR pairing
    details: A signed, single-use, expiring invitation. Each side saves the contact only if the handshake proves both identities.
    link: /guide/pairing
  - icon: 🕵️
    title: Minimal metadata
    details: The server only sees random topics that rotate daily. It doesn't know identities, names, or who talks to whom over time.
    link: /concepts/rendezvous
  - icon: ✅
    title: Honest, reliable delivery
    details: Local outbox, ACK after storing, idempotent receipt, retries with backoff, and ordering by Lamport clocks.
    link: /concepts/outbox-and-acks
  - icon: 🧪
    title: Tested end to end
    details: 120+ tests, including two full devices against the real server with network drops, restarts and unreachable peers.
    link: /guide/testing
---

<div class="vp-doc" style="max-width: 1152px; margin: 64px auto 0; padding: 0 24px;">

## At a glance

<div class="stack-grid">
  <div><strong>App</strong>.NET MAUI (Android first, iOS later), XAML + MVVM with CommunityToolkit.Mvvm</div>
  <div><strong>Cryptography</strong>Noise KK/IK, X25519, Ed25519, ChaCha20-Poly1305, HKDF on top of BouncyCastle</div>
  <div><strong>Storage</strong>SQLite + SQLCipher, key in the Android Keystore, versioned migrations</div>
  <div><strong>Protocol</strong>Strict CBOR between phones, JSON for signaling, public specification</div>
  <div><strong>Server</strong>ASP.NET Core + WebSockets: presence and negotiation relay, no persistence</div>
  <div><strong>Architecture</strong>Clean Architecture, dependency-free domain, ports and small interfaces</div>
  <div><strong>Testing</strong>xUnit, Noise test vectors, parser fuzzing, integration against the real server</div>
  <div><strong>License</strong>AGPL-3.0 for the code, CC BY 4.0 for the specification</div>
</div>

## How it works, in one picture

```mermaid
flowchart LR
    A["📱 Ana<br/>SQLCipher + Keystore"] <== "Encrypted Noise_KK<br/>peer-to-peer" ==> B["📱 Beto<br/>SQLCipher + Keystore"]
    A -. "random topic of the day" .-> S[("Signaling server<br/>no database")]
    B -. "random topic of the day" .-> S
```

The server only helps them **find each other**. Messages go from phone to phone, and only the two
ends can read them.

::: warning Status: pre-alpha and unaudited
Don't use Murmur for sensitive communications until an independent security audit has taken place.
See [Privacy and threats](/guide/privacy).
:::

</div>
