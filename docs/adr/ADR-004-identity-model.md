# ADR-004 — Modelo de identidad

**Estado:** aceptado

## Decisión
- **Identidad**: par Ed25519 (firma). **Estática**: par X25519 (Noise). Una **tarjeta**
  `{identityKey, staticKey, firma}` las une.
- Separar firma y acuerdo de claves evita conversiones Ed25519↔X25519 (XEdDSA) y permite rotar
  la estática o tener una por dispositivo bajo la misma identidad.
- No hay teléfono, email ni usuario global. El contacto se identifica por su clave de identidad.

## Consecuencias
- Multi-dispositivo futuro: varias tarjetas firmadas por la misma identidad.
- Reinstalar = identidad nueva = volver a emparejar (y el código de seguridad cambia).
