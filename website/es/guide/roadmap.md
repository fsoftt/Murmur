# Hoja de ruta

| Fase | Contenido | Estado |
|---|---|---|
| 1. Fundamentos | Identidad, Keystore, SQLCipher, dominio, migraciones | ✅ |
| 2. Emparejamiento | QR firmado, Noise_IK, código de seguridad | ✅ |
| 3. Signaling | WebSocket, presencia, temas rotativos, reconexión, límites | ✅ |
| 4. P2P real | WebRTC DataChannel (binding de `stream-webrtc-android`), SDP cifrado | ⏳ siguiente |
| 5. Mensajes | Noise_KK, outbox, ACK, idempotencia, reintentos, Lamport, contrapresión, estados | ✅ sobre transporte simulado y relay de desarrollo |
| 6. Robustez | Entrega en segundo plano y modo siempre disponible ✅ · cambios de red, métricas de éxito P2P respetuosas con la privacidad ⏳ | 🟡 |
| 7. Adjuntos | Fragmentos, reanudación, integridad, límites | ⏳ |
| — | Auditoría de seguridad independiente | ⏳ antes de cualquier uso real |

## Fuera del MVP, a propósito

Grupos · llamadas · historias · bots · backups en la nube · multi-dispositivo completo · TURN propio ·
Bluetooth · Wi-Fi Direct · federación · directorio global de usuarios.

## Preguntas abiertas

- **¿Cuántas parejas no consiguen ruta directa?** Decide si TURN entra o no.
- **Notificaciones push en iOS:** iOS no permite el trabajo en segundo plano de Android; un aviso push sin contenido despertaría la app, pero APNs vería metadata. Requiere su propio ADR.
- **Recuperación:** ¿copia local exportable cifrada? Nunca una copia en la nube con la clave en el proveedor.
