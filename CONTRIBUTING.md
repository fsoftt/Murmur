# Contribuir a Murmur

## Origen de las contribuciones (DCO)

Cada commit debe incluir `Signed-off-by: Nombre <email>` (`git commit -s`), certificando el
[Developer Certificate of Origin](https://developercertificate.org/).

## Reglas no negociables

1. **No implementar primitivas criptográficas.** Usa `Murmur.Security.Primitives`. Cualquier
   cambio en `Murmur.Security` o en el protocolo necesita revisión explícita y, si cambia el
   formato de cable, actualizar `docs/protocol/spec.md` y los vectores de prueba.
2. **Nunca registrar** texto de mensajes, claves, tokens, invitaciones, temas de encuentro ni IPs.
   Usa `LoggerMessage` con categorías de error, no con datos.
3. **Ninguna dependencia nueva** sin revisar su licencia (compatible con AGPL-3.0 y sin
   restricciones adicionales de uso) y su mantenimiento. Ver ADR-010.
4. **Nada de SDKs de analítica, publicidad o crash reporting.**
5. Todo dato de red es hostil: límites antes de parsear, parsers estrictos, tests de fuzzing.
6. Si una funcionalidad debilita una garantía (E2EE, forward secrecy, local-first, metadata),
   escribe un ADR antes del código.

## Estilo de código

- Clean Architecture: el dominio no depende de infraestructura; las dependencias apuntan hacia él.
- MVVM: las vistas no hacen SQL, criptografía ni red; los ViewModels no conocen el transporte.
- Clases con una sola responsabilidad, interfaces pequeñas, nombres que expresan intención
  (`MarkDeliveredAsync`, no `Process`). Errores de dominio tipados (`MurmurErrorCode`).
- Estado explícito con máquinas de estados, no combinaciones de booleanos.
- Operaciones idempotentes y transaccionales; nada crítico solo en memoria.
- `dotnet format` debe pasar; los avisos son errores en las librerías.

## Pruebas

- Toda corrección de bug llega con un test que falla sin ella.
- Lógica de dominio y protocolo: tests unitarios. Repositorios: SQLCipher real.
- Escenarios entre dispositivos: `tests/Murmur.IntegrationTests` (servidor real + dos clientes completos).
- Ejecuta `dotnet test Murmur.slnx` antes de abrir un PR.

## Decisiones

Las decisiones de arquitectura se registran en `docs/adr/`. Un ADR aceptado no se edita: se
reemplaza por uno nuevo que lo referencia.
