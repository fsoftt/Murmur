# Run it

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- For the app: the MAUI workload (`dotnet workload install maui-android`) and the Android SDK.

## Tests

```bash
git clone https://github.com/fsoftt/Murmur.git
cd Murmur
dotnet test Murmur.slnx
```

`Murmur.slnx` doesn't include the MAUI app, so it builds on any operating system.

## Signaling server

```bash
dotnet run --project src/Murmur.Signaling.Server --urls http://0.0.0.0:8080
# or with Docker
docker build -f src/Murmur.Signaling.Server/Dockerfile -t murmur-signaling .
docker run -p 8080:8080 murmur-signaling
```

In production it sits behind a TLS proxy (Release builds require `wss://`). If the proxy
forwards the client IP, set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` so the per-IP limits
work. Limits are configured in the `Signaling` section of `appsettings.json`.

## App on an emulator or on two phones

1. Start the server as above.
2. Install the **Debug** app: the `murmur-debug-apk` APK from CI, or
   `dotnet build src/Murmur.App -f net10.0-android -t:Run`.
3. The emulator points to `ws://10.0.2.2:8080/ws` by default (your computer's `localhost`).
   On real phones, change the address in **Settings**.
4. Phone 1: **Show my QR**. Phone 2: **Scan QR**. Start chatting.

::: warning Development only
Debug builds use a transport that routes the encrypted channel through the server
([ADR-006](/guide/decisions#adr-006)). Release builds won't connect until WebRTC lands.
:::

## This site

```bash
cd website
npm ci
npm run dev
```
