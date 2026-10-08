# Architecture

Murmur follows **Clean Architecture** with **MVVM** in the presentation layer: dependencies point
toward the domain, and the domain knows nothing about SQLite, Noise, WebSockets or MAUI.

## Projects and dependencies

```mermaid
flowchart TB
    App["Murmur.App<br/><i>XAML views, MAUI services</i>"] --> Presentation
    Presentation["Murmur.Presentation<br/><i>MVVM ViewModels, user-facing text</i>"] --> Client
    Client["Murmur.Client<br/><i>Composition root: MurmurClient</i>"] --> Networking & Storage
    Networking["Murmur.Networking<br/><i>Signaling, P2P links, Noise session,<br/>connections, pairing</i>"] --> Domain & Security
    Storage["Murmur.Storage<br/><i>SQLCipher, migrations, repositories</i>"] --> Domain
    Security["Murmur.Security<br/><i>Noise, identity, invitations,<br/>safety number, topics</i>"] --> Protocol
    Networking --> Protocol
    Protocol["Murmur.Protocol<br/><i>CBOR, signaling JSON, limits</i>"]
    Domain["Murmur.Domain<br/><i>Entities, use cases, outbox/ACK,<br/>state machine, ports</i>"]
    Server["Murmur.Signaling.Server<br/><i>ASP.NET Core + WebSockets</i>"] --> Protocol

    classDef core fill:#1f6feb22,stroke:#1f6feb
    class Domain core
```

| Project | Responsibility | Depends on |
|---|---|---|
| `Murmur.Domain` | Model (`Contact`, `Conversation`, `Message`), rules, use cases (`SendMessage`, `ManageContacts`), delivery engine (`ConversationSyncSession`), state machine, ports (`IMessageRepository`, `IPeerChannel`, `ISecretStore`) | nothing |
| `Murmur.Protocol` | Wire formats: CBOR frames, identity cards, invitations, signaling messages, limits | `System.Formats.Cbor` |
| `Murmur.Security` | Noise KK/IK, local keys, signed invitations, safety number, topic derivation | Protocol, BouncyCastle |
| `Murmur.Storage` | `SqliteDatabase` (SQLCipher), migrations, repositories | Domain |
| `Murmur.Networking` | `SignalingClient`, `IPeerLink` and its implementations, `SecureSession`, `PeerConnectionManager`, `PairingService` | Domain, Protocol, Security |
| `Murmur.Client` | `MurmurClient`: wires together identity, database, networking and use cases | Networking, Storage |
| `Murmur.Presentation` | ViewModels testable on any OS | Client |
| `Murmur.App` | Views, `SecureStorage`, Shell navigation, QR | Presentation |
| `Murmur.Signaling.Server` | Presence and relay over WebSocket | Protocol |

## The domain's ports

The domain defines **what it needs**; the infrastructure decides **how**:

```mermaid
classDiagram
    direction LR
    class IPeerChannel {
      <<interface>>
      SendMessageAsync(Message)
      SendAckAsync(MessageId)
      ReadEventsAsync() PeerEvent*
    }
    class IMessageRepository {
      <<interface>>
      AppendOutgoingAsync(draft) Message
      TryAppendIncomingAsync(message) bool
      ListOutboxAsync(conversation)
      MarkSentAsync / MarkDeliveredAsync
    }
    class ISecretStore {
      <<interface>>
      GetAsync / SetAsync / RemoveAsync
    }
    class ConversationSyncSession {
      RunAsync(conversation, channel)
    }
    ConversationSyncSession ..> IPeerChannel
    ConversationSyncSession ..> IMessageRepository
    SecureSession ..|> IPeerChannel : Noise over IPeerLink
    SqliteMessageRepository ..|> IMessageRepository : SQLCipher
    MauiSecretStore ..|> ISecretStore : Android Keystore
```

Thanks to this, the delivery engine is tested with an in-memory channel and a real SQLCipher
database, with no network, no cryptography and no emulator.

## Runtime components

```mermaid
flowchart LR
    subgraph Phone
      UI[MAUI views] --> VM[ViewModels]
      VM --> DC[MurmurClient]
      BG["OutboxWorker · always-available<br/>service"] --> DC
      DC --> PCM[PeerConnectionManager]
      PCM --> CC1["ContactConnection<br/>(one per contact)"]
      CC1 --> SS[SecureSession<br/>Noise]
      SS --> L[IPeerLink]
      CC1 --> CSS[ConversationSyncSession]
      CSS --> DB[(SQLCipher)]
      DC --> PS[PairingService]
      DC --> SC[SignalingClient]
      PCM --> SC
    end
    SC <-- WebSocket --> SRV[Signaling server]
    L <-- "P2P (WebRTC in phase 4)" --> OTRO[Other phone]
```

- **`PeerConnectionManager`** computes each contact's topics, subscribes to them and creates one
  `ContactConnection` per non-blocked contact.
- **`ContactConnection`** waits for the contact to be present, opens the link, performs the Noise
  handshake and runs the sync. If anything fails, it retries with backoff.
- **`ConversationSyncSession`** drains the outbox, retransmits anything unacknowledged and stores
  what it receives.
- **`ClientHost`** shares a single `MurmurClient` per process between the UI, the WorkManager job
  (`OutboxWorker`) and the "always available" foreground service
  ([ADR-012](/guide/decisions#adr-012)).

## Pluggable transports

```mermaid
flowchart TB
    IPL["IPeerLink<br/>reliable · ordered · message-based"]
    IPL --- W["WebRtcPeerLink<br/><i>phase 4: stream-webrtc-android binding</i>"]
    IPL --- M["InMemoryPeerLinkNetwork<br/><i>tests: simulates NAT and drops</i>"]
    IPL --- D["DevRelayPeerLinkFactory<br/><i>Debug only: through the server relay</i>"]
    IPL --- U["UnavailablePeerLinkFactory<br/><i>Release until phase 4</i>"]
    IPL -.- F["LAN, Bluetooth, Wi-Fi Direct<br/><i>future</i>"]
```

Confidentiality comes from **Noise on top**, so no transport needs to be trusted.

## The signaling server

A single WebSocket endpoint (`/ws`). It keeps in memory which connections are subscribed to which
topic and forwards small opaque blobs. **It has no database.**

| Protection | Default value |
|---|---|
| Topics per connection | 512 |
| Connections per topic | 8 |
| Connections per IP (IPv6 grouped by /64) | 32 |
| Messages per connection | 20/s, burst of 100 |
| Frame size / relay blob size | 32 KiB / 16 KiB |
| Outbound queue | 256 (slow client = disconnected) |
| Time to send `hello` | 10 s |

Membership sits behind `ITopicHub`, so it can scale horizontally with pub/sub without touching
the protocol.
