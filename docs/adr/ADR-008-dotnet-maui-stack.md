# ADR-008 — .NET MAUI, Android primero, Clean Architecture + MVVM

**Estado:** aceptado

## Decisión
- Cliente en **.NET MAUI** (Android ahora, iOS más adelante); servidor en **ASP.NET Core**; .NET 10.
- Sin versión web: MAUI no genera web y una versión Blazor tendría garantías más débiles
  (sin almacén seguro de claves, sin SQLCipher).
- Proyectos: `Domain` (sin dependencias) ← `Protocol`, `Security`, `Storage`, `Networking` ←
  `Client` (composición) ← `Presentation` (ViewModels, .NET puro) ← `App` (vistas MAUI).
- La app compila también para `net10.0` para verificar vistas y servicios en cualquier SO.

## Consecuencias
- ViewModels, dominio, protocolo y servidor se prueban en Linux/CI sin emulador.
- WebRTC requiere bindings nativos por plataforma (ADR-003).
