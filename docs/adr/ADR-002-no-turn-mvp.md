# ADR-002 — Sin TURN en el MVP

**Estado:** aceptado (revisable con datos)

## Contexto
TURN retransmite tráfico cuando no hay ruta directa (CGNAT simétrico, firewalls). Convierte al
servidor en relay de contenido cifrado, con coste de ancho de banda y más metadata.

## Decisión
No desplegar TURN. Si no hay ruta directa, la conexión falla y se reintenta con backoff; los
mensajes siguen pendientes.

## Consecuencias
- Un porcentaje de parejas no conectará nunca en ciertas redes.
- Antes de hacer permanente esta decisión hay que medir (con métricas opt-in sin identificadores
  persistentes) el porcentaje de intentos sin ruta directa.
