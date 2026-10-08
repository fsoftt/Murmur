# ADR-007 — Temas de encuentro por pareja y rotativos

**Estado:** aceptado

## Contexto
La forma ingenua ("¿está B conectado?") enseña al servidor el grafo social completo.

## Decisión
- Tema de contacto = `HKDF(DH(estáticaA, estáticaB), época diaria)`: solo la pareja puede calcularlo.
- Tema de emparejamiento = `HKDF(token del QR)`.
- Rotación diaria con solape de 1 h alrededor de medianoche UTC.

## Consecuencias
- El servidor no ve identidades ni puede enlazar a una pareja entre días solo por el tema.
- Sigue viendo que dos conexiones (IPs) comparten un tema durante el día.
- Un dispositivo con N contactos se suscribe a N (o 2N) temas; el servidor limita a 512 por conexión.
