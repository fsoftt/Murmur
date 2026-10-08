# What is Murmur

**Murmur** is a private messaging app with three properties that don't usually go together:

1. **Local-first.** Your conversations live on your devices, not in anyone's cloud.
2. **End-to-end encrypted.** Only the two phones in the conversation can read the messages.
3. **Peer-to-peer.** Messages travel directly from one phone to the other; the server only helps them find each other.

> **Guiding principle:** if a feature can be done safely on the device,
> it needs a good reason to go to the server.

## What makes it different

| Typical messaging | Murmur |
|---|---|
| You sign up with your phone number or email | Your identity is a key pair generated on your phone |
| The server stores messages until you receive them | There's no mailbox: the message waits **on your phone** |
| The server knows who is whose contact | Contacts exist only on the two phones |
| You add contacts by their number | You pair **in person** by scanning a QR code |
| You get everything back when you switch phones | No recovery in the MVP: what isn't on your phone doesn't exist |

## The rules of the game (product decisions)

::: info Messages travel when both phones are online at the same time
There's no mailbox on the server ([ADR-001](/guide/decisions#adr-001)): until the other person
connects, the message waits on your phone marked as **🕒 pending on this device**. You don't need
to keep the app open: on Android, a background job retries delivery every 15 minutes, and anyone
who turns on **Always available** can receive with the app closed
([ADR-012](/guide/decisions#adr-012), [more details](/guide/life-of-a-message#without-opening-the-app)).
:::

- **Your contacts see your public IP** while you talk: that's the price of a direct connection.
- **No TURN**: on some networks (strict CGNAT, corporate networks) there will be no direct route and
  messages will stay pending ([ADR-002](/guide/decisions#adr-002)).
- **No recovery**: losing your phone means losing your identity, contacts and history.
- **"Delete" means delete on your phone.** Nobody can guarantee deletion on the other one.

## What's included today

- Local identity with keys in the Android Keystore.
- SQLCipher database with migrations.
- QR pairing with a 60-digit safety number.
- Signaling server with rotating topics, presence and anti-abuse limits.
- Noise sessions, outbox, ACKs, idempotency, retries, Lamport ordering and backpressure.
- .NET MAUI app: conversations, chat with honest statuses, QR, scanner, contact details, settings.
- Protocol specification, threat model and 11 ADRs.

The real WebRTC transport is the [next phase](/guide/roadmap). In the meantime, Debug builds
include a development-only transport for testing on real phones.

## What Murmur is not (yet)

Groups, calls, attachments, multi-device, backups and a web version are deliberately left out of the MVP.
First, get the core working well.
