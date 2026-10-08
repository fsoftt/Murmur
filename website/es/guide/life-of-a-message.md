# La vida de un mensaje

Ana escribe "Hola" a Beto. Así viaja ese mensaje, paso a paso.

## 1. Escribir: primero se guarda

```mermaid
sequenceDiagram
    actor Ana
    participant VM as ChatViewModel
    participant UC as SendMessage
    participant DB as SQLCipher (Ana)
    participant SIG as OutboxSignal
    Ana->>VM: Enviar "Hola"
    VM->>UC: ExecuteAsync(contacto, "Hola")
    UC->>UC: valida (no vacío, ≤ 16 KiB, contacto no bloqueado)
    UC->>DB: BEGIN · lamport = lamport + 1 · INSERT (Pending) · COMMIT
    UC-->>VM: evento MessageStored
    VM-->>Ana: 🕒 pendiente en este dispositivo
    UC->>SIG: despierta la sesión de la conversación (si existe)
```

El mensaje existe **antes** de intentar enviarlo. Si la app muere ahora, no se pierde nada.

## 2. Encontrarse

```mermaid
sequenceDiagram
    participant A as Teléfono de Ana
    participant S as Servidor de signaling
    participant B as Teléfono de Beto
    A->>S: hello · sub [tema del día]
    S-->>A: presence(tema, peers = 0)
    Note over A: Beto no está: el mensaje sigue pendiente
    B->>S: hello · sub [mismo tema]
    S-->>A: presence(tema, peers = 1)
    S-->>B: presence(tema, peers = 1)
    Note over A,B: Ambos online: inicia quien tiene la clave estática menor
```

El tema es `HKDF(DH(estáticaAna, estáticaBeto), día)`: solo ellos dos pueden calcularlo.
[Más sobre temas de encuentro](/es/concepts/rendezvous).

## 3. Conectarse y autenticarse

```mermaid
sequenceDiagram
    participant A as Ana (inicia)
    participant B as Beto
    A->>B: enlace P2P (WebRTC en la fase 4)
    A->>B: Noise_KK mensaje 1: e, es, ss + {versiones}
    B->>A: Noise_KK mensaje 2: e, ee, se + {versiones}
    Note over A,B: Dos claves de sesión nuevas · cada uno ha demostrado su identidad<br/>Versión negociada = min(máximos) si ≥ max(mínimos)
```

Si alguien se hace pasar por Beto, el handshake falla: no tiene su clave privada.
[Más sobre Noise](/es/concepts/noise).

## 4. Entregar y confirmar

```mermaid
sequenceDiagram
    participant DA as SQLCipher (Ana)
    participant A as Sesión de Ana
    participant B as Sesión de Beto
    participant DB as SQLCipher (Beto)
    A->>DA: ListOutbox
    A->>B: ChatMessage{id, lamport, hora, "Hola"} (cifrado)
    A->>DA: Pending → Sent
    Note over A: ↑ transmitido, esperando confirmación
    B->>DB: INSERT si no existe (conversación, id) · reloj = max(reloj, lamport)
    B->>A: Ack{id} (solo después de guardar)
    A->>DA: Sent → Delivered
    Note over A: ✓ entregado
```

### ¿Y si algo sale mal?

| Fallo | Qué pasa |
|---|---|
| Se pierde el ACK | Sin ACK en 10 s, Ana reenvía (10 s → 20 s → … → 2 min, con jitter). Beto ya lo tiene: no lo duplica y vuelve a confirmar. |
| Se corta la red | La sesión termina; `ContactConnection` pasa a *Reconnecting* y reintenta. Lo no confirmado se reenvía en la siguiente sesión. |
| Ana cierra la app | El mensaje sigue en su base como *Pending* o *Sent*. Se reenvía al volver. |
| Beto recibe 10 000 mensajes de golpe | Contrapresión: los lee a 20/s tras una ráfaga de 200, sin cortar la sesión. |
| Alguien manipula un byte | ChaCha20-Poly1305 lo detecta, la sesión se cierra y se reconecta. |
| Los relojes de los teléfonos difieren | El orden lo da el reloj de Lamport, no la hora. |

## Sin abrir la app {#sin-abrir-la-app}

Android suspende las apps en segundo plano, así que Murmur se apoya en dos mecanismos
([ADR-012](/es/guide/decisions#adr-012)):

```mermaid
sequenceDiagram
    participant WM as WorkManager (Ana)
    participant CA as Cliente de Ana
    participant SIG as Signaling
    participant FS as Servicio "siempre disponible" (Beto)
    Note over WM: cada 15 min con red,<br/>y al salir de la app
    WM->>CA: OutboxWorker: DeliverPendingAsync(presupuesto)
    CA->>SIG: anuncia temas de encuentro
    FS->>SIG: Beto ya escucha con la app cerrada
    SIG-->>CA: Beto presente
    CA->>FS: Noise_KK · MESSAGE · ACK
    CA-->>WM: 0 pendientes → fin
    FS-->>FS: notificación "Nuevo mensaje" (sin texto)
```

- **Quien envía** no necesita la app abierta: el trabajo arranca el cliente, espera los ACK hasta
  agotar su presupuesto de tiempo y se detiene. Lo que quede pendiente se reintenta en el siguiente.
- **Quien recibe** puede activar **Siempre disponible** en Ajustes: un servicio en primer plano con
  una notificación fija mantiene el teléfono localizable y se reactiva al reiniciar. Sin él, recibe
  la próxima vez que abra la app.
- El servidor sigue sin guardar nada y las notificaciones nunca muestran el texto.

## 5. Estados que ve el usuario

```mermaid
stateDiagram-v2
    [*] --> Pending: guardado localmente
    Pending --> Sent: transmitido en una sesión
    Sent --> Delivered: ACK recibido
    Pending --> Delivered: ACK muy rápido
    Pending --> Failed: rechazado
    Failed --> Pending: reintentar
    Delivered --> [*]
```

🕒 pendiente en este dispositivo · ↑ transmitido, esperando confirmación · ✓ entregado · ! no entregado.
La app **nunca** dice "enviado" cuando el mensaje solo está en tu teléfono.
