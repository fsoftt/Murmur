# Privacidad y amenazas

> Resumen del [modelo de amenazas completo](https://github.com/fsoftt/Murmur/blob/main/docs/threat-model.md).
> **El cifrado de extremo a extremo protege el contenido; no elimina toda la metadata.**

## Qué ve cada uno

| | Contenido | Contactos | Quién habla con quién | Tu IP | Cuándo estás online |
|---|---|---|---|---|---|
| **Servidor de signaling** | ❌ | ❌ | Solo "estas dos conexiones comparten un tema aleatorio hoy" | ✅ | ✅ |
| **Red o Wi-Fi hostil** | ❌ (cifrado y autenticado) | ❌ | Ve tráfico entre dos IPs | ✅ | ✅ |
| **Tu contacto** | ✅ | — | — | ✅ (P2P) | ✅ |
| **Ladrón con el teléfono bloqueado** | ❌ (SQLCipher + Keystore) | ❌ | ❌ | — | — |
| **Malware en tu teléfono desbloqueado** | ✅ | ✅ | ✅ | ✅ | ✅ |

La última fila es honesta: **la seguridad de extremo a extremo termina en los extremos.**

## Lo que el servidor nunca recibe

Texto de mensajes · nombres · claves de identidad · listas de contactos · invitaciones · claves privadas.
Un test de integración graba **todo** el tráfico del servidor durante un emparejamiento y un
intercambio de mensajes, y comprueba que no aparece nada de eso.

## Propiedades criptográficas

- **Confidencialidad e integridad** de cada mensaje (ChaCha20-Poly1305).
- **Autenticación mutua** en cada sesión (Noise_KK con las claves del emparejamiento).
- **Forward secrecy por sesión**: grabar tráfico hoy y robar el teléfono mañana no sirve.
- **Detección de replay y reordenamiento** (nonces de contador).
- Sin *post-compromise security* dentro de una misma sesión ([ADR-005](/es/guide/decisions#adr-005)).

## Higiene

- Nunca se registran mensajes, claves, tokens, invitaciones, temas ni IPs.
- Sin SDKs de analítica, publicidad ni crash reporting.
- Copias de seguridad de Android desactivadas.
- Los avisos de error usan lenguaje claro, nunca trazas técnicas.

## Prioridades de la auditoría

1. Implementación de Noise.
2. Emparejamiento y verificación de invitaciones.
3. Parsers de red y límites.
4. Almacenamiento local y gestión de claves.
5. Servidor de signaling (abuso y metadata).
