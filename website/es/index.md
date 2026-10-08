---
layout: home

hero:
  name: Murmur
  text: Mensajería privada que no necesita un servidor para guardar tus conversaciones
  tagline: Sin cuentas, sin teléfono, sin email. Tus contactos se añaden en persona con un código QR, los mensajes viajan cifrados de teléfono a teléfono y el historial vive solo en tus dispositivos.
  image:
    src: /favicon.svg
    alt: Murmur
  actions:
    - theme: brand
      text: Qué es Murmur
      link: /es/guide/overview
    - theme: alt
      text: Arquitectura
      link: /es/guide/architecture
    - theme: alt
      text: Ver el código en GitHub
      link: https://github.com/fsoftt/Murmur

features:
  - icon: 📱
    title: Local-first
    details: El historial y los mensajes pendientes viven en una base de datos cifrada en tu teléfono. Ningún servidor guarda copia.
    link: /es/guide/life-of-a-message
  - icon: 🔐
    title: Cifrado de extremo a extremo con Noise
    details: Cada conexión entre dos teléfonos abre una sesión Noise_KK nueva, con forward secrecy. Validado byte a byte contra una implementación independiente.
    link: /es/concepts/noise
  - icon: 🤝
    title: Emparejamiento por QR
    details: Invitación firmada, de un solo uso y con caducidad. Ambos lados guardan el contacto solo si el handshake demuestra las dos identidades.
    link: /es/guide/pairing
  - icon: 🕵️
    title: Metadata mínima
    details: El servidor solo ve temas aleatorios que rotan cada día. No conoce identidades, nombres ni quién habla con quién a largo plazo.
    link: /es/concepts/rendezvous
  - icon: ✅
    title: Entrega honesta y fiable
    details: Outbox local, ACK después de guardar, recepción idempotente, reintentos con backoff y orden por relojes de Lamport.
    link: /es/concepts/outbox-and-acks
  - icon: 🧪
    title: Probado de punta a punta
    details: 120+ tests, incluidos dos dispositivos completos contra el servidor real con caídas de red, reinicios y peers inalcanzables.
    link: /es/guide/testing
---

<div class="vp-doc" style="max-width: 1152px; margin: 64px auto 0; padding: 0 24px;">

## De un vistazo

<div class="stack-grid">
  <div><strong>App</strong>.NET MAUI (Android primero, iOS después), XAML + MVVM con CommunityToolkit.Mvvm</div>
  <div><strong>Criptografía</strong>Noise KK/IK, X25519, Ed25519, ChaCha20-Poly1305, HKDF sobre BouncyCastle</div>
  <div><strong>Almacenamiento</strong>SQLite + SQLCipher, clave en Android Keystore, migraciones versionadas</div>
  <div><strong>Protocolo</strong>CBOR estricto entre teléfonos, JSON en el signaling, especificación pública</div>
  <div><strong>Servidor</strong>ASP.NET Core + WebSockets: presencia y relay de negociación, sin persistencia</div>
  <div><strong>Arquitectura</strong>Clean Architecture, dominio sin dependencias, puertos e interfaces pequeñas</div>
  <div><strong>Pruebas</strong>xUnit, vectores de Noise, fuzzing de parsers, integración con servidor real</div>
  <div><strong>Licencia</strong>AGPL-3.0 para el código, CC BY 4.0 para la especificación</div>
</div>

## Cómo funciona, en una imagen

```mermaid
flowchart LR
    A["📱 Ana<br/>SQLCipher + Keystore"] <== "Noise_KK cifrado<br/>peer-to-peer" ==> B["📱 Beto<br/>SQLCipher + Keystore"]
    A -. "tema aleatorio del día" .-> S[("Servidor de signaling<br/>sin base de datos")]
    B -. "tema aleatorio del día" .-> S
```

El servidor solo les ayuda a **encontrarse**. Los mensajes van de teléfono a teléfono y únicamente
los dos extremos pueden leerlos.

::: warning Estado: pre-alfa y sin auditar
No uses Murmur para comunicaciones sensibles hasta que exista una auditoría de seguridad
independiente. Consulta [Privacidad y amenazas](/es/guide/privacy).
:::

</div>
