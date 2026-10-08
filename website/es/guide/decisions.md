# Decisiones de diseño

Cada decisión importante está registrada como un ADR en
[`docs/adr`](https://github.com/fsoftt/Murmur/tree/main/docs/adr), con su contexto, las alternativas
y las consecuencias. Este es el resumen.

## ADR-001 · Local-first, sin buzón en el servidor {#adr-001}
La fuente de verdad es la base de cada dispositivo. Un mensaje solo se entrega cuando **los dos
teléfonos coinciden** conectados. *Precio:* peor experiencia que con un buzón central, suavizada por
la entrega en segundo plano ([ADR-012](#adr-012)).

## ADR-002 · Sin TURN en el MVP {#adr-002}
No se retransmite tráfico por un servidor cuando no hay ruta directa. *Precio:* algunas parejas no
conectarán nunca. Se medirá antes de hacerlo permanente.

## ADR-003 · Transporte abstracto, WebRTC como objetivo {#adr-003}
El dominio solo ve `IPeerLink`. Producción: WebRTC DataChannel vía binding de `stream-webrtc-android`.
**SIPSorcery descartado** por una restricción de uso en su licencia incompatible con la AGPL.

## ADR-004 · Modelo de identidad {#adr-004}
Ed25519 para firmar + X25519 estática para Noise, unidas en una tarjeta firmada. Sin teléfono, email
ni usuario global. Preparado para varias tarjetas por usuario (multi-dispositivo).

## ADR-005 · Noise por conexión en lugar de Double Ratchet {#adr-005}
Sin buzón asíncrono, un handshake Noise_KK nuevo por conexión da *forward secrecy* sin estado de
ratchet persistente. Implementación propia sobre primitivas auditadas, validada con vectores
independientes y prioridad nº 1 de la auditoría.

## ADR-006 · Transporte por relay solo para desarrollo {#adr-006}
Para probar en teléfonos antes de WebRTC, las compilaciones Debug pasan el canal (ya cifrado) por el
relay del servidor, con ritmo limitado. Release no lo incluye.

## ADR-007 · Temas de encuentro por pareja y rotativos {#adr-007}
`HKDF(DH(estáticas), día UTC)`: el servidor no ve identidades ni puede seguir a una pareja entre días
solo por el tema.

## ADR-008 · .NET MAUI, Android primero, Clean Architecture + MVVM {#adr-008}
Sin versión web (garantías más débiles). ViewModels en .NET puro para probarlos en cualquier SO.

## ADR-009 · SQLCipher con clave en el almacén seguro {#adr-009}
Clave aleatoria de 256 bits en el Keystore, modo de clave cruda. La app se niega a abrir una base sin cifrar.

## ADR-010 · Licencias {#adr-010}
AGPL-3.0 para el código (los derivados, incluido un servidor modificado ofrecido como servicio,
siguen siendo auditables), CC BY 4.0 para la especificación, DCO para las contribuciones.

## ADR-011 · Orden por relojes de Lamport {#adr-011}
Orden total `(lamport, creación, id)`, idéntico en ambos teléfonos e inmune al desfase de relojes.

## ADR-012 · Entrega en segundo plano y "siempre disponible" {#adr-012}
**A.** Un trabajo de WorkManager (cada 15 minutos con red, y otro al salir de la app) arranca el
cliente y vacía el outbox: quien envía ya no tiene que abrir la app. **B.** Un servicio en primer
plano opcional, con su notificación fija, mantiene el teléfono localizable con la app cerrada y se
reactiva al reiniciar. Las notificaciones nunca muestran el texto del mensaje y el servidor sigue
sin guardar nada. *Precio:* algo más de batería con B; iOS necesitará otra solución.
