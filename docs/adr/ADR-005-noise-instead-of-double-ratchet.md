# ADR-005 — Noise KK/IK por conexión en lugar de Double Ratchet

**Estado:** aceptado

## Contexto
La arquitectura inicial proponía Double Ratchet. Double Ratchet resuelve la mensajería
**asíncrona**: cifrar para alguien offline y descifrar mensajes que llegan desordenados o mucho
después, a través de un buzón. Directo, por diseño (ADR-001), solo entrega cuando ambos están
conectados en una sesión fiable y ordenada.

## Decisión
- Cada conexión ejecuta un handshake **Noise** nuevo (`Noise_KK_25519_ChaChaPoly_SHA256`);
  el emparejamiento usa `Noise_IK`.
- Los mensajes pendientes no se cifran por adelantado para el contacto: se guardan en la base
  cifrada y se cifran al transmitirse.
- Noise se implementa transcribiendo la especificación pública (rev. 34) sobre primitivas
  auditadas de BouncyCastle y .NET (no se implementan primitivas). Se valida byte a byte contra
  la implementación independiente `noiseprotocol` (Python).

## Consecuencias
- *Forward secrecy* por sesión sin estado de ratchet persistente: menos superficie de errores
  (pérdida de estado, desincronización, mensajes saltados).
- No hay *post-compromise security* dentro de una misma sesión (las sesiones son cortas).
- La implementación de Noise es código propio y es la primera prioridad de la auditoría. Si se
  añade un buzón asíncrono en el futuro, habrá que revisar este ADR (MLS o Double Ratchet).
