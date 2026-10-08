# Protocolo Directo — versión 1

> Especificación independiente de la implementación, para que puedan existir clientes
> compatibles en otras tecnologías. Licencia: CC BY 4.0 (ver `docs/LICENSE-CC-BY-4.0.txt`).
> Las palabras DEBE, NO DEBE y PUEDE tienen el sentido de RFC 2119.

## 1. Primitivas

| Uso | Primitiva |
|---|---|
| Firma de identidad | Ed25519 (RFC 8032) |
| Acuerdo de claves | X25519 (RFC 7748). Un resultado todo-ceros DEBE rechazarse. |
| Sesiones | Noise Protocol Framework rev. 34: `Noise_KK_25519_ChaChaPoly_SHA256` y `Noise_IK_25519_ChaChaPoly_SHA256` |
| Derivación de temas | HKDF-SHA256 (RFC 5869) |
| Código de seguridad | SHA-512 iterado |

## 2. Codificación

- Estructuras binarias: **CBOR** (RFC 8949), mapas de longitud definida con **claves enteras sin signo**.
  El emisor DEBE usar codificación canónica. El receptor DEBE rechazar claves duplicadas, bytes
  sobrantes y tamaños fuera de límites, y DEBE **ignorar claves desconocidas** (compatibilidad hacia delante).
- Ids de mensaje: 16 bytes (UUID, orden de bytes big-endian). Tiempos: milisegundos Unix (`int64`).

### Límites (DEBEN comprobarse antes de reservar memoria)

| Elemento | Máximo |
|---|---|
| Cuerpo de mensaje (UTF-8) | 16 384 bytes |
| Frame de aplicación (texto plano) | 61 440 bytes |
| Mensaje Noise / mensaje de enlace | 65 535 bytes |
| Payload de handshake | 2 048 bytes |
| Invitación decodificada | 1 024 bytes |
| Nombre de perfil (UTF-8) | 64 bytes |
| Capacidades | 32 × 64 caracteres |

## 3. Tarjeta de identidad

```
IdentityCard = { 0: 1 (versión), 1: identityKey (bstr 32, Ed25519),
                 2: staticKey (bstr 32, X25519), 3: signature (bstr 64) }
signature = Ed25519(identityKey, "Directo/v1/identity-card" || identityKey || staticKey)
```

## 4. Invitación (contenido del QR)

```
texto        = "DIRECTO1:" || base64url_sin_relleno(Envelope)
Envelope     = { 0: bodyBytes (bstr), 1: signature (bstr 64) }
body         = { 0: 1 (versión), 1: IdentityCard (bstr CBOR), 2: token (bstr 16),
                 3: expiresAt (segundos Unix), 4?: profileName (tstr) }
signature    = Ed25519(card.identityKey, "Directo/v1/invite" || bodyBytes)
```

El verificador DEBE comprobar la firma de la tarjeta, la firma de la invitación sobre los bytes
exactos recibidos, que `expiresAt + 5 min ≥ ahora` y que `expiresAt ≤ ahora + 24 h + 5 min`.
La invitación NO DEBE contener claves privadas ni secretos permanentes.

## 5. Temas de encuentro (rendezvous)

Un tema son 32 bytes en base64url sin relleno (43 caracteres, codificación canónica).

```
epoch          = floor(unixSeconds / 86400)
contactTopic   = HKDF-SHA256(ikm = X25519(miEstática, suEstática),
                             salt = "Directo/v1/rendezvous/contact",
                             info = int64_be(epoch), L = 32)
pairingTopic   = HKDF-SHA256(ikm = token, salt = "Directo/v1/rendezvous/pairing", info = "", L = 32)
```

Un cliente DEBE suscribirse a la época actual y, si está a menos de 1 h de un cambio de día UTC,
también a la vecina. Para negociar, ambos lados eligen el **menor** tema (orden ordinal) en el
que el otro está presente.

## 6. Protocolo de signaling (WebSocket, tramas de texto JSON)

Discriminador `t`. Máximo 32 KiB por trama; los blobs de relay, máximo 16 KiB decodificados.

| `t` | Dirección | Campos |
|---|---|---|
| `hello` | C→S | `v` (versión, 1). DEBE ser el primer mensaje (timeout 10 s). |
| `welcome` | S→C | `v`, `minV` |
| `sub` / `unsub` | C→S | `topics` (1–256 temas) |
| `presence` | S→C | `topic`, `peers` = número de **otras** conexiones suscritas |
| `relay` | C↔S | `topic`, `data` (base64url). Solo si el emisor está suscrito; se reenvía a los demás miembros. |
| `error` | S→C | `code` ∈ `bad_request`, `unsupported_version`, `rate_limited`, `too_many_topics`, `topic_full`, `not_subscribed`, `payload_too_large` |
| `ping` / `pong` | C↔S | — |

El servidor NO DEBE persistir temas, relays ni direcciones más allá de la vida de la conexión.
El relay existe para negociar transportes P2P (p. ej. SDP de WebRTC), **no** para mensajes.

## 7. Enlace entre pares

Un enlace es un canal **fiable, ordenado y orientado a mensajes** (p. ej. WebRTC DataChannel
fiable y ordenado). Exactamente un lado inicia: el de clave estática lexicográficamente menor
(contactos) o el que escanea (emparejamiento). El enlace no necesita confidencialidad propia.

## 8. Handshakes

Cada mensaje Noise viaja como un mensaje del enlace. Los payloads de handshake son:

```
HandshakePayload = { 0: minVersion, 1: maxVersion, 2: [capabilities...],
                     3?: IdentityCard (bstr CBOR), 4?: pairingToken (bstr 16), 5?: profileName }
```

Versión negociada = `min(maxA, maxB)`, que DEBE ser `≥ max(minA, minB)`; si no, se aborta.

### 8.1 Sesión de contacto — `Noise_KK`, prólogo `"Directo/v1/contact"`

Ambos conocen la clave estática del otro desde el emparejamiento. Payloads: solo versiones.

### 8.2 Emparejamiento — `Noise_IK`, prólogo `"Directo/v1/pairing"`

- Mensaje 1 (escáner → invitador): payload con `IdentityCard`, `pairingToken` y `profileName` opcional.
- El invitador DEBE verificar: token igual (tiempo constante), no caducado, no consumido por otra
  identidad; firma de la tarjeta; y que la clave estática **probada por el handshake** es la de la
  tarjeta. Solo entonces guarda el contacto y envía el mensaje 2 (versiones + nombre opcional).
  Si algo falla, NO DEBE responder.
- El escáner guarda el contacto al recibir el mensaje 2. Repetir con el mismo token y la misma
  identidad DEBE aceptarse (idempotente).

## 9. Transporte de aplicación

Tras el handshake, cada mensaje del enlace es un mensaje de transporte Noise cuyo texto plano es:

```
ChatMessage = { 0: 1, 1: messageId (bstr 16), 2: lamport (uint), 3: sentAt (ms), 4: body (tstr) }
Ack         = { 0: 2, 1: messageId (bstr 16) }
```

Tipos desconocidos DEBEN ignorarse. Un fallo de autenticación o de formato DEBE cerrar la sesión.

### 9.1 Reglas de entrega

- El emisor persiste antes de transmitir. "Transmitido" ≠ "entregado".
- El receptor DEBE persistir el mensaje **antes** de enviar `Ack`, y DEBE enviar `Ack` también
  para duplicados (recepción idempotente por `(conversación, messageId)`).
- Un `Ack` solo marca como entregados mensajes **salientes** de **esa** conversación.
- Sin `Ack`, el emisor retransmite con backoff exponencial y jitter (por defecto 10 s → 2 min).

### 9.2 Orden

Relojes de Lamport por conversación: el emisor usa `reloj + 1`; el receptor aplica
`reloj = max(reloj, lamport)`, acotando `lamport` a `reloj + 1 000 000`. Orden total de
visualización: `(lamport, createdAt, messageId)`.

## 10. Código de seguridad

Para cada clave de identidad `K`: `h = SHA-512(0x00 0x01 || K || "Directo")`, después 5 200 veces
`h = SHA-512(h || K)`. Se toman 6 bloques de 5 bytes (big-endian) módulo 100 000 → 30 dígitos.
El código son los 30 dígitos de ambas partes concatenados en orden ascendente, en grupos de 5.

## 11. Versionado

- `protocolVersion` se negocia en cada handshake; tarjetas e invitaciones llevan su propia versión.
- Nuevos campos = nuevas claves CBOR (ignoradas por clientes antiguos).
- Cambios incompatibles = nueva versión de protocolo y nuevos prólogos.
