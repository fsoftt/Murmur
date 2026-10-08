# Architecture Decision Records

| ADR | Decisión |
|---|---|
| [001](ADR-001-local-first.md) | Local-first: sin buzón en servidor; entrega solo con ambos online |
| [002](ADR-002-no-turn-mvp.md) | Sin TURN en el MVP |
| [003](ADR-003-peer-transport.md) | Transporte P2P abstracto; WebRTC DataChannel como objetivo; SIPSorcery descartado |
| [004](ADR-004-identity-model.md) | Identidad Ed25519 + clave estática X25519 en una tarjeta firmada |
| [005](ADR-005-noise-instead-of-double-ratchet.md) | Noise KK/IK por conexión en lugar de Double Ratchet |
| [006](ADR-006-dev-relay-transport.md) | Transporte por relay solo para desarrollo |
| [007](ADR-007-rotating-rendezvous.md) | Temas de encuentro por pareja, rotativos |
| [008](ADR-008-dotnet-maui-stack.md) | .NET MAUI, Android primero, Clean Architecture + MVVM |
| [009](ADR-009-sqlcipher-storage.md) | SQLCipher con clave en Keystore |
| [010](ADR-010-licensing.md) | AGPL-3.0 para código, CC BY 4.0 para la especificación |
| [011](ADR-011-lamport-ordering.md) | Orden por relojes de Lamport |
| [012](ADR-012-background-delivery.md) | Entrega en segundo plano y modo "siempre disponible" |

Formato: Contexto · Decisión · Consecuencias. Un ADR aceptado no se edita; se reemplaza por otro.
