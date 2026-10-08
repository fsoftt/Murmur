# Vectores de prueba

## El problema del código criptográfico

Un error en criptografía no suele romper nada visible: el programa cifra y descifra perfectamente…
consigo mismo. Si nuestro Noise tuviera un error sutil (un orden de bytes, una clave mal mezclada),
dos teléfonos con **el mismo** código se entenderían igual, y el error pasaría desapercibido.

## La solución

Un **vector de prueba** es una entrada fija con su salida exacta conocida, producida por **otra
implementación independiente**. Si la nuestra produce exactamente los mismos bytes, implementa la
especificación y no una variante propia.

```mermaid
flowchart LR
    K["Claves fijas<br/>(estáticas y efímeras)"] --> PY["noiseprotocol<br/>(Python, independiente)"]
    K --> CS["Directo.Security<br/>(C#)"]
    PY --> V1[mensajes del handshake,<br/>hash, transporte]
    CS --> V2[mensajes del handshake,<br/>hash, transporte]
    V1 --> EQ{¿Idénticos<br/>byte a byte?}
    V2 --> EQ
```

Los vectores de Directo cubren **Noise_KK** y **Noise_IK**: los dos mensajes del handshake, el hash
del handshake y tres mensajes de transporte en ambas direcciones. El script que los generó está en
el repositorio, para que cualquiera pueda reproducirlos.

## Lo que complementa a los vectores

- Tests negativos: prólogo distinto, clave equivocada, bit cambiado, replay, mensaje fuera de turno.
- Fuzzing de los parsers.
- Y, antes de cualquier uso real, **una auditoría independiente**.

**En el código:** [`NoiseVectorTests.cs`](https://github.com/fsoftt/Directo/blob/main/tests/Directo.Security.Tests/NoiseVectorTests.cs) ·
[`noise-vectors.json`](https://github.com/fsoftt/Directo/blob/main/tests/Directo.Security.Tests/TestVectors/noise-vectors.json) ·
[`generate_noise_vectors.py`](https://github.com/fsoftt/Directo/blob/main/tests/Directo.Security.Tests/TestVectors/generate_noise_vectors.py)
