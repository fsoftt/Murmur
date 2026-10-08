# ADR-003 — Transporte P2P abstracto y WebRTC DataChannel

**Estado:** aceptado

## Contexto
Necesitamos atravesar NAT (ICE/STUN) y un canal fiable y ordenado. En .NET MAUI no hay WebRTC
integrado.

## Decisión
- El dominio y la capa segura dependen solo de `IPeerLink` (fiable, ordenado, por mensajes). La
  confidencialidad la da Noise por encima, no el transporte.
- Implementación de producción (fase 4): **WebRTC DataChannel** mediante un *binding* .NET de
  `io.getstream:stream-webrtc-android` (Apache-2.0, compilación de libwebrtc de Google). Para iOS,
  un binding de WebRTC.framework (BSD).
- **SIPSorcery descartado**: su licencia actual añade a la BSD una restricción de uso por motivos
  geopolíticos. Eso la saca de la definición de open source y es incompatible con la AGPL-3.0,
  que prohíbe restricciones adicionales.
- El SDP se intercambia por el relay de signaling. Mejora prevista: cifrar el SDP con una clave
  derivada del secreto de la pareja para no revelar IPs locales al servidor.

## Consecuencias
- El binding de Android requiere el SDK de Android y validación en dispositivos reales.
- Otros transportes (LAN, Bluetooth) se añaden sin tocar el dominio.
