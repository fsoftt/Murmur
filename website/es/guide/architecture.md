# Arquitectura

Murmur sigue **Clean Architecture** con **MVVM** en la presentación: las dependencias apuntan hacia
el dominio y el dominio no conoce SQLite, Noise, WebSockets ni MAUI.

## Proyectos y dependencias

```mermaid
flowchart TB
    App["Murmur.App<br/><i>Vistas XAML, servicios MAUI</i>"] --> Presentation
    Presentation["Murmur.Presentation<br/><i>ViewModels MVVM, textos para el usuario</i>"] --> Client
    Client["Murmur.Client<br/><i>Raíz de composición: MurmurClient</i>"] --> Networking & Storage
    Networking["Murmur.Networking<br/><i>Signaling, enlaces P2P, sesión Noise,<br/>conexiones, emparejamiento</i>"] --> Domain & Security
    Storage["Murmur.Storage<br/><i>SQLCipher, migraciones, repositorios</i>"] --> Domain
    Security["Murmur.Security<br/><i>Noise, identidad, invitaciones,<br/>código de seguridad, temas</i>"] --> Protocol
    Networking --> Protocol
    Protocol["Murmur.Protocol<br/><i>CBOR, JSON de signaling, límites</i>"]
    Domain["Murmur.Domain<br/><i>Entidades, casos de uso, outbox/ACK,<br/>máquina de estados, puertos</i>"]
    Server["Murmur.Signaling.Server<br/><i>ASP.NET Core + WebSockets</i>"] --> Protocol

    classDef core fill:#1f6feb22,stroke:#1f6feb
    class Domain core
```

| Proyecto | Responsabilidad | Depende de |
|---|---|---|
| `Murmur.Domain` | Modelo (`Contact`, `Conversation`, `Message`), reglas, casos de uso (`SendMessage`, `ManageContacts`), motor de entrega (`ConversationSyncSession`), máquina de estados, puertos (`IMessageRepository`, `IPeerChannel`, `ISecretStore`) | nada |
| `Murmur.Protocol` | Formatos de cable: frames CBOR, tarjetas de identidad, invitaciones, mensajes de signaling, límites | `System.Formats.Cbor` |
| `Murmur.Security` | Noise KK/IK, claves locales, invitaciones firmadas, código de seguridad, derivación de temas | Protocol, BouncyCastle |
| `Murmur.Storage` | `SqliteDatabase` (SQLCipher), migraciones, repositorios | Domain |
| `Murmur.Networking` | `SignalingClient`, `IPeerLink` y sus implementaciones, `SecureSession`, `PeerConnectionManager`, `PairingService` | Domain, Protocol, Security |
| `Murmur.Client` | `MurmurClient`: ensambla identidad, base de datos, red y casos de uso | Networking, Storage |
| `Murmur.Presentation` | ViewModels testeables en cualquier SO | Client |
| `Murmur.App` | Vistas, `SecureStorage`, navegación Shell, QR | Presentation |
| `Murmur.Signaling.Server` | Presencia y relay sobre WebSocket | Protocol |

## Los puertos del dominio

El dominio define **qué necesita**; la infraestructura decide **cómo**:

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
    SecureSession ..|> IPeerChannel : Noise sobre IPeerLink
    SqliteMessageRepository ..|> IMessageRepository : SQLCipher
    MauiSecretStore ..|> ISecretStore : Android Keystore
```

Gracias a esto, el motor de entrega se prueba con un canal en memoria y una base SQLCipher real,
sin red, sin criptografía y sin emulador.

## Componentes en tiempo de ejecución

```mermaid
flowchart LR
    subgraph Teléfono
      UI[Vistas MAUI] --> VM[ViewModels]
      VM --> DC[MurmurClient]
      BG["OutboxWorker · servicio<br/>siempre disponible"] --> DC
      DC --> PCM[PeerConnectionManager]
      PCM --> CC1["ContactConnection<br/>(una por contacto)"]
      CC1 --> SS[SecureSession<br/>Noise]
      SS --> L[IPeerLink]
      CC1 --> CSS[ConversationSyncSession]
      CSS --> DB[(SQLCipher)]
      DC --> PS[PairingService]
      DC --> SC[SignalingClient]
      PCM --> SC
    end
    SC <-- WebSocket --> SRV[Servidor de signaling]
    L <-- "P2P (WebRTC en fase 4)" --> OTRO[Otro teléfono]
```

- **`PeerConnectionManager`** calcula los temas de cada contacto, se suscribe y crea una
  `ContactConnection` por contacto no bloqueado.
- **`ContactConnection`** espera a que el contacto esté presente, abre el enlace, hace el handshake
  Noise y ejecuta la sincronización. Si algo falla, reintenta con backoff.
- **`ConversationSyncSession`** vacía el outbox, retransmite lo no confirmado y guarda lo recibido.
- **`ClientHost`** comparte un único `MurmurClient` por proceso entre la interfaz, el trabajo de
  WorkManager (`OutboxWorker`) y el servicio en primer plano "siempre disponible"
  ([ADR-012](/es/guide/decisions#adr-012)).

## Transportes intercambiables

```mermaid
flowchart TB
    IPL["IPeerLink<br/>fiable · ordenado · por mensajes"]
    IPL --- W["WebRtcPeerLink<br/><i>fase 4: binding de stream-webrtc-android</i>"]
    IPL --- M["InMemoryPeerLinkNetwork<br/><i>tests: simula NAT y caídas</i>"]
    IPL --- D["DevRelayPeerLinkFactory<br/><i>solo Debug: por el relay del servidor</i>"]
    IPL --- U["UnavailablePeerLinkFactory<br/><i>Release hasta la fase 4</i>"]
    IPL -.- F["LAN, Bluetooth, Wi-Fi Direct<br/><i>futuro</i>"]
```

La confidencialidad la pone **Noise por encima**, así que ningún transporte necesita ser de confianza.

## El servidor de signaling

Un único endpoint WebSocket (`/ws`). Mantiene en memoria qué conexiones están suscritas a qué tema
y reenvía pequeños blobs opacos. **No tiene base de datos.**

| Protección | Valor por defecto |
|---|---|
| Temas por conexión | 512 |
| Conexiones por tema | 8 |
| Conexiones por IP (IPv6 agrupado por /64) | 32 |
| Mensajes por conexión | 20/s, ráfaga de 100 |
| Tamaño de trama / blob de relay | 32 KiB / 16 KiB |
| Cola de salida | 256 (cliente lento = desconectado) |
| Tiempo para enviar `hello` | 10 s |

La membresía está detrás de `ITopicHub`, para poder escalar horizontalmente con pub/sub sin tocar
el protocolo.
