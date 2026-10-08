# Cómo se construyó

El proyecto partió de un documento de arquitectura escrito antes de una sola línea de código
([original](https://github.com/fsoftt/Murmur/blob/main/docs/history/arquitectura-inicial.md)).
Se construyó en pasos pequeños y verificables, siguiendo una regla:

> **Cada capa se prueba antes de construir la siguiente encima.**

## 1. Revisar el diseño antes de programar

La revisión del documento inicial cambió varias decisiones:

- **Noise en lugar de Double Ratchet.** Double Ratchet existe para mensajería asíncrona con buzón.
  Murmur solo entrega con ambos online, así que un handshake nuevo por conexión da *forward
  secrecy* con mucha menos complejidad ([ADR-005](/es/guide/decisions#adr-005)).
- **Emparejamiento bidireccional.** El flujo inicial no explicaba cómo conoce el invitador al que
  escanea; se resolvió con Noise_IK.
- **Presencia privada desde el MVP**, no "en el futuro": temas derivados por pareja y rotativos.
- **Decisiones de producto explícitas**: sin buzón (entrega cuando los teléfonos coinciden), IP visible para los contactos, sin recuperación.

## 2. Protocolo y criptografía primero

1. Formatos de cable CBOR con límites antes de parsear y claves desconocidas ignoradas.
2. Noise transcrito de la especificación sobre primitivas de BouncyCastle.
3. **Vectores de prueba generados con una implementación independiente** (Python `noiseprotocol`):
   nuestra implementación produce **los mismos bytes** en cada mensaje del handshake y del transporte.
4. Fuzzing: 20 000 entradas aleatorias o con un bit cambiado no pueden producir otra cosa que un
   error de protocolo controlado.

## 3. El dominio, sin infraestructura

Entidades, casos de uso y el motor de entrega se escribieron contra interfaces. El motor se probó
con un canal en memoria con inyección de fallos (ACK perdidos, cortes, reinicios) y una base
SQLCipher real.

## 4. Almacenamiento cifrado

SQLCipher con clave cruda de 256 bits desde el almacén seguro. Un test abre el fichero de la base
y comprueba que no contiene ni el texto de los mensajes ni la cabecera de SQLite.

## 5. Red, servidor y emparejamiento

Servidor ASP.NET Core con límites anti-abuso, cliente con reconexión y resuscripción, sesiones
Noise sobre una abstracción de transporte y el gestor de conexiones con su máquina de estados.
Los tests de integración levantan **el servidor real en proceso y dos dispositivos completos**.

## 6. Presentación y app

ViewModels en .NET puro (testeables en Linux) y vistas MAUI encima. La app compila también para
`net10.0`, lo que permite verificar el XAML sin el SDK de Android.

## Hallazgos por el camino

- **SIPSorcery descartado.** La librería WebRTC en C# más conocida añadió a su licencia BSD una
  restricción de uso geopolítica. Eso la saca del open source y es incompatible con la AGPL, así que
  la fase 4 usará un binding de `stream-webrtc-android` (Apache-2.0) ([ADR-003](/es/guide/decisions#adr-003)).
- **Un test intermitente** destapó una carrera en la red simulada de los tests (no en el producto):
  la simulación de "inalcanzable" no afectaba a intentos ya en curso.
- **El relay de desarrollo se desincronizaba** con muchos mensajes: el servidor descartaba tramas
  por su límite de ritmo y Noise, con nonces de contador, no tolera huecos. Se añadió un limitador
  propio; el test de 80 mensajes falla sin él y pasa con él.
- **`Base64Url.TryDecodeFromChars` lanza excepción** con entradas no canónicas en lugar de devolver
  `false`: lo encontró un test de validación de temas.
