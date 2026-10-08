# Modelo de amenazas — Directo (MVP)

> Estado: **no auditado**. No debe presentarse como "seguro" hasta completar la auditoría
> externa (revisión criptográfica, protocolo, cliente, servidor, metadata).

## Activos

Contenido de los mensajes · claves privadas de identidad y estáticas · clave de la base de
datos · lista de contactos · grafo social (quién habla con quién) · IP y horarios.

## Adversarios y qué se espera

| Adversario | Protección | Notas |
|---|---|---|
| **Servidor de signaling curioso o comprometido** | No ve contenido, nombres, claves de identidad ni contactos. | Ve IPs, horarios, duración de conexiones y qué conexiones comparten un tema aleatorio (puede inferir que dos IPs son pareja durante un día). Puede negar servicio. |
| **Red hostil / Wi-Fi público** | Contenido cifrado con Noise (ChaChaPoly) y autenticado; replay y reordenamiento detectados. | Ve que hay tráfico P2P entre dos IPs. |
| **Atacante activo en signaling** | Cambiar SDP/relays solo provoca fallos de conexión: Noise_KK autentica ambos extremos. | Denegación de servicio posible. |
| **Contacto (par legítimo)** | — | **Ve tu IP pública** (consecuencia de P2P). Puede capturar pantalla, exportar o modificar su cliente. |
| **Quien ve tu QR de lejos** | Token de un solo uso, caducidad corta (10 min), el contacto queda "sin verificar". | Si escanea antes que la persona legítima, se empareja él. Mitigación: comparar el **código de seguridad**. |
| **Robo del teléfono bloqueado** | Base SQLCipher; clave y claves privadas en Android Keystore. | Depende de la seguridad del SO y del bloqueo de pantalla. |
| **Dispositivo desbloqueado o con malware** | **Fuera de alcance.** | La seguridad de extremo a extremo termina en los extremos. |
| **Par malicioso que envía datos hostiles** | Límites antes de parsear, CBOR estricto, reloj Lamport acotado, ACK solo sobre salientes de esa conversación, sesión cerrada ante fallos. | Puede inundar con mensajes válidos (pendiente: límites por contacto). |
| **Abuso del servidor** | Límites por conexión, por IP (/64 en IPv6), por tema, token bucket, cola acotada. | Un atacante con muchas IPs aún puede saturar. |

## Propiedades criptográficas

- **Confidencialidad e integridad** por sesión (Noise transport, ChaChaPoly).
- **Autenticación mutua** por claves estáticas conocidas desde el emparejamiento (KK).
- **Forward secrecy por sesión**: cada conexión usa efímeras nuevas; comprometer la clave estática
  más tarde no descifra sesiones grabadas.
- **Sin post-compromise security dentro de una sesión** (no hay ratchet); una sesión nueva sí
  recupera la seguridad frente a un atacante pasivo si la clave estática no está comprometida.
- **Los mensajes pendientes se guardan en claro dentro de la base cifrada** hasta su entrega;
  se cifran para el contacto en el momento de transmitir.
- Con la clave estática comprometida, un atacante activo puede suplantar (inherente a KK).

## Fugas conocidas de metadata

IP frente al servidor y frente a los contactos · correlación temporal de dos conexiones en un
tema · tamaño y ritmo del tráfico P2P · presencia (cuándo tienes la app abierta).

## Riesgos aceptados en el MVP

- Sin TURN: algunas parejas nunca conectan.
- Sin recuperación de identidad ni backups.
- Transporte de relay **solo en compilaciones Debug** ([ADR-006](adr/ADR-006-dev-relay-transport.md)):
  si se usa, el servidor ve el tamaño y ritmo del tráfico cifrado.
- Implementación propia de Noise sobre primitivas de BouncyCastle, validada contra una implementación
  independiente; prioridad nº 1 de la auditoría.

## Higiene

Nunca se registran: texto de mensajes, claves, tokens, invitaciones, temas, IPs (servidor).
No hay SDKs de analítica ni de crash reporting.
