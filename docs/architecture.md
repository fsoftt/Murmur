# Arquitectura — Murmur

> Mensajería privada **local-first**, **cifrada de extremo a extremo** y **peer-to-peer**.
> Este documento es la versión revisada de la arquitectura inicial. Las decisiones concretas y
> sus alternativas están en [`adr/`](adr/); el formato de cable, en
> [`protocol/spec.md`](protocol/spec.md); los riesgos, en [`threat-model.md`](threat-model.md).

Principio rector:

> **Si una funcionalidad puede hacerse de forma segura en el dispositivo, necesita una buena
> razón para ir al servidor.** Los servidores no deben poseer conversaciones ni claves privadas
> para que el producto funcione.

---

## 1. Decisiones de producto explícitas

| Tema | Decisión | Consecuencia que el usuario debe entender |
|---|---|---|
| Entrega | Solo cuando **ambos dispositivos están online a la vez** ([ADR-001](adr/ADR-001-local-first.md)) | Si escribes y cierras la app antes de que el otro se conecte, el mensaje espera en **tu** teléfono. |
| Segundo plano | En el MVP la app entrega mensajes **mientras está abierta**. Android e iOS cortan los sockets en segundo plano. | En la práctica, ambos deben abrir la app en algún momento común. Las notificaciones push se estudiarán aparte por la metadata que exponen. |
| Relay | **Sin TURN** en el MVP ([ADR-002](adr/ADR-002-no-turn-mvp.md)) | Algunas redes (CGNAT simétrico, redes corporativas) nunca conectarán; los mensajes quedan pendientes. Se medirá antes de decidir. |
| IP | P2P significa que **cada contacto ve tu IP pública** mientras habláis. | Diferencia de privacidad importante frente a apps con servidor central; se explica en el onboarding. |
| Recuperación | **Sin recuperación** en el MVP. | Perder el teléfono = perder identidad, contactos e historial. |
| Borrado | "Borrar para mí" únicamente. | No existe borrado remoto garantizado. |
| Plataformas | Android primero (.NET MAUI); iOS después; **sin web**. | [ADR-008](adr/ADR-008-dotnet-maui-stack.md) |

---

## 2. Modelo mental

```
QR          = quién eres             (claves públicas firmadas, nunca secretos)
Signaling   = ¿dónde estás ahora?    (temas opacos y rotativos, nada persistente)
P2P         = hablamos directamente  (canal fiable y ordenado entre dispositivos)
Noise       = solo nosotros leemos   (sesión cifrada nueva en cada conexión)
SQLite      = si no estás, espero    (outbox local cifrado con SQLCipher)
```

---

## 3. Componentes y capas

```
┌──────────────────────────── Murmur.App (MAUI, Android) ───────────────────────────┐
│  Views (XAML)  ──►  Murmur.Presentation (ViewModels MVVM, textos para el usuario)  │
└──────────────────────────────────────┬──────────────────────────────────────────────┘
                                       ▼
                     Murmur.Client (raíz de composición: MurmurClient)
              ┌────────────────────────┼─────────────────────────────┐
              ▼                        ▼                             ▼
     Murmur.Domain            Murmur.Networking               Murmur.Storage
  entidades, casos de uso,   signaling, IPeerLink, sesión      SQLCipher, migraciones,
  outbox/ACK, máquina de     Noise, gestor de conexiones,      repositorios
  estados, puertos           emparejamiento
              ▲                        │
              │                        ▼
              │               Murmur.Security ──► Murmur.Protocol
              │          Noise KK/IK, identidad,      CBOR (frames, invitaciones),
              │          invitaciones, safety number  JSON de signaling, límites
              │
   (el dominio no depende de nada; todo apunta hacia él)

Murmur.Signaling.Server (ASP.NET Core) ──► Murmur.Protocol
```

- **Domain** no conoce SQLite, Noise ni WebRTC: habla con `IPeerChannel`, repositorios y `ISecretStore`.
- **Transporte intercambiable** (`IPeerLink`): WebRTC (fase 4), LAN/Bluetooth en el futuro,
  red en memoria para tests y un relay solo de desarrollo ([ADR-006](adr/ADR-006-dev-relay-transport.md)).
- **Signaling y mensajería separados**: el canal de signaling nunca transporta mensajes en producción.

---

## 4. Identidad

Cada instalación genera ([ADR-004](adr/ADR-004-identity-model.md)):

- una clave **Ed25519 de identidad** (firma), y
- una clave **X25519 estática** (handshakes Noise),

unidas por una **tarjeta de identidad** firmada. Las claves privadas viven solo en el
almacenamiento seguro de la plataforma (Android Keystore vía `SecureStorage`). No hay cuenta,
email, teléfono ni contraseña central.

Multi-dispositivo (futuro): cada dispositivo tendrá su propia tarjeta firmada por la identidad
del usuario. El outbox ya es por conversación y la clave del contacto es la tarjeta, lo que
permite pasar a "una entrega por dispositivo" sin rediseñar el dominio.

---

## 5. Emparejamiento por QR (bidireccional)

1. **B** genera una invitación: tarjeta + token aleatorio de un solo uso + caducidad (10 min) +
   nombre opcional, **firmada** con su identidad. B guarda el token y escucha en el tema
   `HKDF(token)`.
2. **A** escanea, verifica firma y caducidad **sin red**, y se suscribe al mismo tema.
3. Cuando ambos están presentes se abre un enlace P2P y se ejecuta **Noise_IK**: A conoce la
   clave estática de B por el QR; A envía cifrados su tarjeta y el token.
4. B comprueba token (tiempo constante, no caducado, no usado por otra identidad), firma de la
   tarjeta y que la clave estática probada en el handshake coincide con la tarjeta. **Solo
   entonces** guarda el contacto y responde.
5. A guarda a B al recibir la respuesta. Si la respuesta se pierde, A puede reintentar con el
   mismo QR (el token queda ligado a su identidad).

Ambos pueden comparar después el **código de seguridad** (60 dígitos, algoritmo tipo Signal).

---

## 6. Encontrarse sin revelar el grafo social

- Cada pareja deriva un **tema de encuentro** de su secreto X25519 compartido:
  `HKDF(DH(sA, sB), salt="Murmur/v1/rendezvous/contact", info=día UTC)` ([ADR-007](adr/ADR-007-rotating-rendezvous.md)).
- El tema **rota cada día**; cerca de medianoche se suscriben dos días para tolerar desfase de reloj.
- El servidor ve "dos conexiones en el mismo tema aleatorio", nunca identidades ni nombres, y
  no puede enlazar la misma pareja de un día a otro solo por el tema.
- Sigue viendo IPs y horarios: E2EE protege contenido, no toda la metadata (ver modelo de amenazas).

---

## 7. Sesiones y entrega

1. El gestor de conexiones mantiene una máquina de estados por contacto:
   `Disconnected → Discovering → Negotiating → Connected → Reconnecting`.
2. Rol determinista: inicia el dispositivo con la clave estática menor (evita colisiones).
3. Enlace P2P + **Noise_KK** con efímeras nuevas → *forward secrecy* por sesión ([ADR-005](adr/ADR-005-noise-instead-of-double-ratchet.md)).
4. Sobre la sesión, `ConversationSyncSession`:
   - envía el outbox en orden Lamport y marca `Sent` (transmitido, **no** entregado);
   - el receptor guarda de forma **idempotente** (clave `(conversación, id)`) y **solo después** envía `ACK`;
   - sin ACK se retransmite con backoff exponencial + jitter; los duplicados se ACKean de nuevo;
   - `Delivered` solo al recibir el ACK.
5. Cualquier fallo de autenticación o de formato cierra la sesión; se reintenta con backoff.

Orden: **relojes de Lamport** ([ADR-011](adr/ADR-011-lamport-ordering.md)), con desempate por
fecha de creación e id, idéntico en ambos dispositivos. Un par hostil no puede adelantar el
reloj más de 10⁶ por mensaje.

Estados visibles: 🕒 pendiente en este dispositivo · ↑ transmitido, esperando confirmación · ✓ entregado · ! no entregado.

---

## 8. Almacenamiento local

- SQLite + **SQLCipher** con clave aleatoria de 256 bits guardada en el Keystore, nunca junto a la base ([ADR-009](adr/ADR-009-sqlcipher-storage.md)).
- La app **se niega a arrancar** si SQLCipher no está disponible (no hay modo sin cifrar).
- Migraciones versionadas con `PRAGMA user_version`, tablas `STRICT`, claves foráneas en cascada,
  `secure_delete`, WAL y `synchronous=FULL`.
- Copias de seguridad de Android desactivadas (`allowBackup=false`, reglas de extracción).

---

## 9. Servidor de signaling

ASP.NET Core, un solo endpoint WebSocket (`/ws`) y `/healthz`.

- Solo conoce temas opacos y blobs de relay opacos. **No persiste nada.**
- Límites: temas por conexión, miembros por tema (2 contactos + margen), conexiones por IP (IPv6 por /64),
  token bucket de mensajes, cola de salida acotada (cliente lento = desconectado), tamaño de trama, timeout de `hello`.
- Logs mínimos: nunca IPs, temas ni contenido.
- `ITopicHub` abstrae la membresía para poder escalar horizontalmente con pub/sub.

---

## 10. Ciclo de vida móvil

Al abrir la app: restaurar estado local → abrir base cifrada → conectar signaling → derivar
temas → reconstruir presencia → abrir sesiones → vaciar outbox. Ningún estado crítico vive solo
en memoria; matar la app en cualquier punto no pierde mensajes (cubierto por tests de reinicio).

---

## 11. Hoja de ruta

| Fase | Estado |
|---|---|
| 1. Fundamentos (identidad, Keystore, SQLCipher, dominio, migraciones) | ✅ |
| 2. Emparejamiento QR + código de seguridad | ✅ |
| 3. Signaling (WebSocket, presencia, reconexión, límites) | ✅ |
| 4. P2P real (WebRTC DataChannel vía binding de `stream-webrtc-android`) | ⏳ siguiente |
| 5. Mensajes E2EE, outbox, ACK, idempotencia, reintentos, estados UI | ✅ (sobre transporte simulado y relay de desarrollo) |
| 6. Robustez en dispositivos reales (segundo plano, cambios de red, métricas) | ⏳ |
| 7. Adjuntos por fragmentos con reanudación | ⏳ |

Fuera del MVP: grupos, llamadas, backups, multi-dispositivo completo, TURN propio, Bluetooth,
Wi-Fi Direct, directorio de usuarios.

---

## 12. Checklist para nuevas funcionalidades

1. ¿Puede hacerse localmente? 2. ¿Qué datos nuevos conocería el servidor, por qué y durante
cuánto tiempo? 3. ¿Qué pasa si el servidor o un dispositivo se compromete? 4. ¿Qué metadata
crea? 5. ¿Rompe E2EE o forward secrecy? 6. ¿Sobrevive a matar la app? 7. ¿Cómo se prueba,
versiona y migra? 8. ¿Cómo falla sin perder mensajes?

Si debilita una garantía fundamental, es una decisión de arquitectura (ADR), no un detalle.
