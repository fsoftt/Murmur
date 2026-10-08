# Formatos de cable

El **formato de cable** (*wire format*) es cómo se convierten los datos en bytes para viajar por la
red, y cómo se reconstruyen al otro lado. Si dos programas no coinciden exactamente en él, no se
entienden. Por eso Murmur tiene una
[especificación pública](https://github.com/fsoftt/Murmur/blob/main/docs/protocol/spec.md), independiente del código: con ella se podría
escribir otro cliente compatible, en Kotlin o Swift.

## Dos formatos, dos usos

| | JSON | CBOR |
|---|---|---|
| Dónde | Teléfono ↔ servidor de signaling | Teléfono ↔ teléfono (dentro de Noise) |
| Por qué | Legible y fácil de depurar; el servidor solo ve sobres | Binario, compacto, estricto y con tipos (bytes, enteros) |

### JSON (signaling)

```json
{"t":"sub","topics":["q3Jk0…"]}
{"t":"presence","topic":"q3Jk0…","peers":1}
```

### CBOR (mensajes)

CBOR es "JSON binario". En lugar de nombres de campo usamos **números pequeños**:

```text
{ 0: 1,                 ← tipo: mensaje de chat
  1: h'01a1…',          ← id (16 bytes)
  2: 42,                ← reloj de Lamport
  3: 1791500000000,     ← hora en milisegundos
  4: "Hola" }           ← cuerpo
```

## Las reglas que lo hacen robusto

1. **Límites antes de parsear.** Un frame no puede superar 60 KiB ni un mensaje 16 KiB. Se comprueba
   antes de reservar memoria.
2. **Estricto.** Se rechazan claves duplicadas, bytes sobrantes, longitudes indefinidas y UTF-8 inválido.
3. **Canónico al escribir.** El mismo dato siempre produce los mismos bytes.
4. **Ignorar lo desconocido.** Un campo `5` añadido en una versión futura no rompe a los clientes
   antiguos; un tipo de frame desconocido se ignora. Así el protocolo puede evolucionar.
5. **Todo lo que llega de la red es hostil.** Los tests alimentan los parsers con 20 000 entradas
   aleatorias o con un bit cambiado: solo pueden producir un error de protocolo controlado.

## Versiones

Las tarjetas y las invitaciones llevan su propia versión, el handshake negocia la del protocolo y
el prefijo del QR (`MURMUR1:`) identifica el formato. Un cambio incompatible exige una versión
nueva y un prólogo nuevo.

**En el código:** [`PeerFrameCodec.cs`](https://github.com/fsoftt/Murmur/blob/main/src/Murmur.Protocol/Frames/PeerFrameCodec.cs) ·
[`CborMap.cs`](https://github.com/fsoftt/Murmur/blob/main/src/Murmur.Protocol/Serialization/CborMap.cs) ·
[`SignalingMessages.cs`](https://github.com/fsoftt/Murmur/blob/main/src/Murmur.Protocol/Signaling/SignalingMessages.cs) ·
[`ProtocolConstants.cs`](https://github.com/fsoftt/Murmur/blob/main/src/Murmur.Protocol/ProtocolConstants.cs)
