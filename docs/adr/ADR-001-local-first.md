# ADR-001 — Local-first, sin buzón en servidor

**Estado:** aceptado

## Contexto
Queremos que el historial y los mensajes pendientes pertenezcan a los dispositivos y que el
servidor conozca lo mínimo.

## Decisión
- La fuente de verdad es la base local de cada participante.
- No existe copia de mensajes en ningún servidor. Un mensaje se entrega **solo si emisor y
  receptor están online a la vez**; mientras tanto vive en el outbox cifrado del emisor.

## Consecuencias
- Infraestructura mínima y nada que filtrar desde un buzón central.
- Peor experiencia que una app con servidor: si los horarios no coinciden, el mensaje espera.
  Con las restricciones de segundo plano de Android/iOS, en la práctica ambos deben abrir la app.
- La UI debe ser honesta: "pendiente en este dispositivo" ≠ "enviado".
