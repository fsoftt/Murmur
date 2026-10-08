# The life of a message

Ana writes "Hi" to Beto. Here's how that message travels, step by step.

## 1. Writing: it's stored first

```mermaid
sequenceDiagram
    actor Ana
    participant VM as ChatViewModel
    participant UC as SendMessage
    participant DB as SQLCipher (Ana)
    participant SIG as OutboxSignal
    Ana->>VM: Send "Hi"
    VM->>UC: ExecuteAsync(contact, "Hi")
    UC->>UC: validates (not empty, ≤ 16 KiB, contact not blocked)
    UC->>DB: BEGIN · lamport = lamport + 1 · INSERT (Pending) · COMMIT
    UC-->>VM: MessageStored event
    VM-->>Ana: 🕒 pending on this device
    UC->>SIG: wakes the conversation's session (if any)
```

The message exists **before** any attempt to send it. If the app dies now, nothing is lost.

## 2. Finding each other

```mermaid
sequenceDiagram
    participant A as Ana's phone
    participant S as Signaling server
    participant B as Beto's phone
    A->>S: hello · sub [topic of the day]
    S-->>A: presence(topic, peers = 0)
    Note over A: Beto isn't here: the message stays pending
    B->>S: hello · sub [same topic]
    S-->>A: presence(topic, peers = 1)
    S-->>B: presence(topic, peers = 1)
    Note over A,B: Both online: whoever has the lower static key initiates
```

The topic is `HKDF(DH(staticAna, staticBeto), day)`: only the two of them can compute it.
[More on rendezvous topics](/concepts/rendezvous).

## 3. Connecting and authenticating

```mermaid
sequenceDiagram
    participant A as Ana (initiator)
    participant B as Beto
    A->>B: P2P link (WebRTC in phase 4)
    A->>B: Noise_KK message 1: e, es, ss + {versions}
    B->>A: Noise_KK message 2: e, ee, se + {versions}
    Note over A,B: Two fresh session keys · each side has proven its identity<br/>Negotiated version = min(maximums) if ≥ max(minimums)
```

If someone impersonates Beto, the handshake fails: they don't have his private key.
[More on Noise](/concepts/noise).

## 4. Delivering and acknowledging

```mermaid
sequenceDiagram
    participant DA as SQLCipher (Ana)
    participant A as Ana's session
    participant B as Beto's session
    participant DB as SQLCipher (Beto)
    A->>DA: ListOutbox
    A->>B: ChatMessage{id, lamport, time, "Hi"} (encrypted)
    A->>DA: Pending → Sent
    Note over A: ↑ transmitted, awaiting confirmation
    B->>DB: INSERT if not exists (conversation, id) · clock = max(clock, lamport)
    B->>A: Ack{id} (only after storing)
    A->>DA: Sent → Delivered
    Note over A: ✓ delivered
```

### What if something goes wrong?

| Failure | What happens |
|---|---|
| The ACK is lost | With no ACK within 10 s, Ana resends (10 s → 20 s → … → 2 min, with jitter). Beto already has it: he doesn't duplicate it and acknowledges again. |
| The network drops | The session ends; `ContactConnection` moves to *Reconnecting* and retries. Anything unacknowledged is resent in the next session. |
| Ana closes the app | The message stays in her database as *Pending* or *Sent*. It's resent when she comes back. |
| Beto receives 10,000 messages at once | Backpressure: he reads them at 20/s after a burst of 200, without dropping the session. |
| Someone tampers with a byte | ChaCha20-Poly1305 detects it, the session is closed and reconnects. |
| The phones' clocks disagree | Ordering comes from the Lamport clock, not the wall-clock time. |

## Without opening the app {#without-opening-the-app}

Android suspends apps in the background, so Murmur relies on two mechanisms
([ADR-012](/guide/decisions#adr-012)):

```mermaid
sequenceDiagram
    participant WM as WorkManager (Ana)
    participant CA as Ana's client
    participant SIG as Signaling
    participant FS as Always-available service (Beto)
    Note over WM: every 15 min with network,<br/>and when leaving the app
    WM->>CA: OutboxWorker: DeliverPendingAsync(budget)
    CA->>SIG: announces rendezvous topics
    FS->>SIG: Beto is already listening with the app closed
    SIG-->>CA: Beto present
    CA->>FS: Noise_KK · MESSAGE · ACK
    CA-->>WM: 0 pending → done
    FS-->>FS: "New message" notification (no text)
```

- **The sender** doesn't need the app open: the job starts the client, waits for ACKs until its
  time budget runs out and then stops. Anything still pending is retried on the next run.
- **The recipient** can turn on **Always available** in Settings: a foreground service with a
  persistent notification keeps the phone reachable and comes back after a reboot. Without it,
  they receive the next time they open the app.
- The server still stores nothing, and notifications never show the message text.

## 5. Statuses the user sees

```mermaid
stateDiagram-v2
    [*] --> Pending: stored locally
    Pending --> Sent: transmitted in a session
    Sent --> Delivered: ACK received
    Pending --> Delivered: very fast ACK
    Pending --> Failed: rejected
    Failed --> Pending: retry
    Delivered --> [*]
```

🕒 pending on this device · ↑ transmitted, awaiting confirmation · ✓ delivered · ! not delivered.
The app **never** says "sent" when the message is only on your phone.
