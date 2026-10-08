# Directo

Mensajería privada **local-first**, **cifrada de extremo a extremo** y **peer-to-peer**.

- Sin cuentas, teléfono ni email: tu identidad es una clave criptográfica que nunca sale del teléfono.
- Los contactos se añaden **en persona, escaneando un código QR**.
- El historial y los mensajes pendientes viven **solo en tus dispositivos**, en una base de datos cifrada.
- El servidor solo ayuda a que dos teléfonos se encuentren: no guarda mensajes, contactos ni claves.

> ⚠️ **Estado: pre-alfa, sin auditar.** No uses Directo para comunicaciones sensibles hasta que
> exista una auditoría de seguridad independiente. Ver [`docs/threat-model.md`](docs/threat-model.md).

## Cómo funciona

Un mensaje se entrega **solo cuando emisor y receptor están online a la vez**; hasta entonces
espera cifrado en el teléfono de quien lo escribió. Cada conexión entre dos teléfonos abre una
sesión [Noise](https://noiseprotocol.org) nueva (`Noise_KK_25519_ChaChaPoly_SHA256`) con
*forward secrecy*. Los detalles están en [`docs/architecture.md`](docs/architecture.md).

## Estructura

| Proyecto | Responsabilidad |
|---|---|
| `src/Directo.Domain` | Entidades, casos de uso, outbox/ACK, máquina de estados. Sin dependencias. |
| `src/Directo.Protocol` | Formatos de cable (CBOR, JSON de signaling) y límites. |
| `src/Directo.Security` | Noise KK/IK, identidad, invitaciones firmadas, código de seguridad, temas de encuentro. |
| `src/Directo.Storage` | SQLite + SQLCipher, migraciones, repositorios. |
| `src/Directo.Networking` | Cliente de signaling, transportes P2P, sesiones seguras, emparejamiento. |
| `src/Directo.Client` | Raíz de composición (`DirectoClient`). |
| `src/Directo.Presentation` | ViewModels MVVM (.NET puro, testeables). |
| `src/Directo.App` | App .NET MAUI (Android). |
| `src/Directo.Signaling.Server` | Servidor de signaling ASP.NET Core. |
| `tests/` | Unitarios, vectores de Noise, integración con servidor real y dos dispositivos completos. |

## Compilar y probar

Requisitos: .NET SDK 10.

```bash
dotnet test Directo.slnx
```

La solución `Directo.slnx` no incluye la app MAUI para que compile en cualquier SO. Para la app:

```bash
dotnet workload install maui-android
dotnet build src/Directo.App -f net10.0-android            # requiere Android SDK
dotnet build src/Directo.App -f net10.0                    # verificación de vistas en cualquier SO
```

## Probar en un emulador o en dos teléfonos

El transporte WebRTC llega en la fase 4. Mientras tanto, las compilaciones **Debug** incluyen un
transporte *solo de desarrollo* que pasa el canal ya cifrado por el servidor
([ADR-006](docs/adr/ADR-006-dev-relay-transport.md)).

1. Arranca el servidor: `dotnet run --project src/Directo.Signaling.Server --urls http://0.0.0.0:8080`
2. Instala la app Debug (artefacto `directo-debug-apk` de la CI o `dotnet build -t:Run`).
   El emulador de Android apunta por defecto a `ws://10.0.2.2:8080/ws`; en teléfonos reales
   cambia el servidor en **Ajustes**.
3. En un teléfono: **Mostrar mi QR**. En el otro: **Escanear QR**.

## Servidor de signaling

```bash
docker build -f src/Directo.Signaling.Server/Dockerfile -t directo-signaling .
docker run -p 8080:8080 directo-signaling
```

Ponlo detrás de un proxy TLS (los clientes Release exigen `wss://`). Si el proxy reenvía la IP
del cliente, activa `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` para que los límites por IP
funcionen. Límites configurables en la sección `Signaling` de `appsettings.json`.

## Documentación

- [Arquitectura](docs/architecture.md) · [Especificación del protocolo](docs/protocol/spec.md) ·
  [Modelo de amenazas](docs/threat-model.md) · [ADRs](docs/adr/README.md)
- [Cómo contribuir](CONTRIBUTING.md) · [Política de seguridad](SECURITY.md)
- Documento de arquitectura original: [`docs/history/arquitectura-inicial.md`](docs/history/arquitectura-inicial.md)

## Licencia

Código bajo **GNU AGPL-3.0-only** ([`LICENSE`](LICENSE)). Especificación y documentación bajo
**CC BY 4.0** ([`docs/LICENSE-CC-BY-4.0.txt`](docs/LICENSE-CC-BY-4.0.txt)).
