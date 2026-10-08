# Política de seguridad

Directo está en **pre-alfa y no ha sido auditado**.

## Informar de una vulnerabilidad

No abras un issue público. Usa la función **"Report a vulnerability"** (GitHub Security
Advisories) de este repositorio. Incluye pasos para reproducir y el impacto que esperas.

Prioridades de revisión, en orden:

1. Implementación de Noise (`src/Directo.Security/Noise`).
2. Emparejamiento y verificación de invitaciones.
3. Parsers de red (CBOR, JSON de signaling) y límites.
4. Almacenamiento local (SQLCipher, gestión de claves).
5. Servidor de signaling (abuso, denegación de servicio, metadata).

Consulta [`docs/threat-model.md`](docs/threat-model.md) para saber qué protege y qué no.
