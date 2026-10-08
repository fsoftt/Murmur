# ADR-010 — Licencias

**Estado:** aceptado

## Decisión
- Código (app, librerías, servidor): **AGPL-3.0-only**. Garantiza que cualquier derivado —incluido
  un servidor de signaling modificado ofrecido como servicio— siga siendo auditable, como hace Signal.
- Especificación del protocolo y documentación: **CC BY 4.0**, para facilitar clientes compatibles.
- Contribuciones bajo **DCO** (`Signed-off-by`), para mantener clara la procedencia.
- Dependencias: solo licencias compatibles con AGPL (MIT, BSD sin restricciones adicionales,
  Apache-2.0, MPL-2.0). Se revisa cada dependencia nueva (ver ADR-003 sobre SIPSorcery).

## Consecuencias
- Algunas empresas evitan AGPL; se acepta.
- La App Store tiene fricciones con (A)GPL; el titular del copyright puede publicar igualmente.
  Las contribuciones de terceros complican relicenciar, de ahí el DCO desde el inicio.
