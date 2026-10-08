# ADR-011 — Orden por relojes de Lamport

**Estado:** aceptado

## Decisión
- Cada conversación mantiene un reloj de Lamport persistido. Enviar: `reloj + 1`. Recibir:
  `reloj = max(reloj, recibido)`, acotando lo recibido a `reloj + 10⁶`.
- Orden total: `(lamport, createdAt, id)`; ambos dispositivos muestran el mismo orden.
- El reloj de pared solo se usa para mostrar la hora.

## Consecuencias
- Inmune al desfase de relojes entre teléfonos (cubierto por tests de escrituras concurrentes).
- Un par malicioso no puede llevar el reloj al desbordamiento.
