# ADR-009 — SQLCipher con clave en el almacén seguro

**Estado:** aceptado

## Decisión
- SQLite con **SQLCipher** (`SQLitePCLRaw.bundle_e_sqlcipher`), clave **aleatoria de 256 bits**
  aplicada en modo clave cruda (`PRAGMA key = "x'…'"`), guardada en `SecureStorage` (Android Keystore).
- Si SQLCipher no está disponible (`PRAGMA cipher_version` vacío), la app se niega a abrir la base.
- Una única conexión serializada: la carga es mínima en móvil y simplifica las transacciones.

## Consecuencias
- Robar el fichero sin la clave del Keystore no revela contenido (cubierto por un test que busca
  texto plano en el fichero).
- Perder el Keystore (p. ej. restaurar el teléfono) hace la base ilegible: coherente con
  "sin recuperación" en el MVP.
