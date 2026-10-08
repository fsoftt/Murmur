# Ejecutarlo

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Para la app: workload MAUI (`dotnet workload install maui-android`) y el SDK de Android.

## Pruebas

```bash
git clone https://github.com/fsoftt/Murmur.git
cd Murmur
dotnet test Murmur.slnx
```

`Murmur.slnx` no incluye la app MAUI, para que compile en cualquier sistema operativo.

## Servidor de signaling

```bash
dotnet run --project src/Murmur.Signaling.Server --urls http://0.0.0.0:8080
# o con Docker
docker build -f src/Murmur.Signaling.Server/Dockerfile -t murmur-signaling .
docker run -p 8080:8080 murmur-signaling
```

En producción va detrás de un proxy TLS (las compilaciones Release exigen `wss://`). Si el proxy
reenvía la IP del cliente, activa `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` para que funcionen los
límites por IP. Los límites se configuran en la sección `Signaling` de `appsettings.json`.

## App en un emulador o en dos teléfonos

1. Arranca el servidor como arriba.
2. Instala la app **Debug**: el APK `murmur-debug-apk` de la CI, o
   `dotnet build src/Murmur.App -f net10.0-android -t:Run`.
3. El emulador apunta por defecto a `ws://10.0.2.2:8080/ws` (el `localhost` de tu ordenador).
   En teléfonos reales cambia la dirección en **Ajustes**.
4. Teléfono 1: **Mostrar mi QR**. Teléfono 2: **Escanear QR**. Escribid.

::: warning Solo desarrollo
Las compilaciones Debug usan un transporte que pasa el canal cifrado por el servidor
([ADR-006](/guide/decisions#adr-006)). Las Release no se conectan hasta que llegue WebRTC.
:::

## Este sitio

```bash
cd website
npm ci
npm run dev
```
