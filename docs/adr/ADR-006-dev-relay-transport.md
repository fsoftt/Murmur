# ADR-006 — Transporte por relay solo para desarrollo

**Estado:** aceptado (temporal)

## Contexto
Hasta tener el transporte WebRTC (fase 4) la app no podría emparejar ni chatear en teléfonos reales.

## Decisión
- `DevRelayPeerLinkFactory` tuneliza el enlace (ya cifrado de extremo a extremo con Noise) por el
  relay del servidor de signaling, fragmentando en bloques de 12 KiB.
- Solo se registra en compilaciones **Debug** (`DIRECTO_DEV_RELAY`). Las compilaciones Release
  usan `UnavailablePeerLinkFactory`: nunca conectan y los mensajes quedan pendientes.

## Consecuencias
- Permite probar emparejamiento, cifrado, persistencia y UI en dispositivos ya.
- Mientras se usa, el servidor ve tamaño y ritmo del tráfico cifrado.
- El enlace limita su propio ritmo (15 tramas/s, ráfaga de 60) por debajo del límite del servidor:
  una trama descartada desincronizaría el transporte Noise y cortaría la sesión.
- Debe eliminarse o quedar solo para tests cuando exista WebRTC.
