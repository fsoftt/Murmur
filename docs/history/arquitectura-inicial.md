# Arquitectura maestra --- Chat P2P privado, local-first y E2EE

> **Estado:** documento de arquitectura inicial\
> **Objetivo:** definir una aplicación de mensajería privada donde el
> historial y los mensajes pendientes pertenezcan a los dispositivos de
> los usuarios, el servidor conozca lo mínimo posible y la comunicación
> sea P2P cuando ambos extremos estén disponibles.

------------------------------------------------------------------------

## 1. Visión del producto

La aplicación será un sistema de mensajería **local-first**,
**end-to-end encrypted (E2EE)** y preferentemente **peer-to-peer
(P2P)**.

Principio rector:

> **Si una funcionalidad puede realizarse de forma segura en el
> dispositivo del usuario, debe existir una buena razón para trasladarla
> al servidor.**

El servidor no será la fuente de verdad de las conversaciones. Su
función principal será ayudar a que dos dispositivos se encuentren y
establezcan una conexión.

### Propiedades buscadas

-   Sin almacenamiento permanente de mensajes en servidores.
-   Historial almacenado localmente y cifrado.
-   Mensajes pendientes almacenados en el dispositivo del emisor.
-   Envío automático cuando ambos dispositivos coincidan online.
-   Comunicación directa P2P siempre que la red lo permita.
-   E2EE: únicamente los extremos poseen las claves necesarias para leer
    mensajes.
-   Identidades criptográficas en lugar de depender obligatoriamente de
    teléfono/email.
-   Vinculación de contactos mediante QR.
-   Servidor incapaz de descifrar conversaciones.
-   Reducción deliberada de metadata.
-   Arquitectura preparada para multi-dispositivo, LAN y transporte
    alternativo en el futuro.
-   Código abierto/protocolo documentado como objetivo deseable.

------------------------------------------------------------------------

# 2. Modelo mental sencillo

Hay cuatro conceptos principales.

## 2.1 QR = «Quién eres»

Cada instalación genera una identidad criptográfica local.

``` text
Dispositivo A
Identity Public Key: A...

Dispositivo B
Identity Public Key: B...
```

El QR permite intercambiar/verificar información pública de identidad y
material de emparejamiento.

**Nunca debe incluir una clave privada.**

## 2.2 Signaling = «¿Dónde estás ahora?»

Las IP cambian. La identidad criptográfica no.

Un pequeño servicio de signaling ayuda a que dos identidades actualmente
conectadas intercambien la información temporal necesaria para intentar
encontrarse en Internet.

## 2.3 P2P = «Ahora hablamos directamente»

Una vez establecida la conexión:

``` text
Dispositivo A  <======== E2EE/P2P ========>  Dispositivo B
```

Los mensajes no necesitan atravesar nuestro backend de aplicación.

## 2.4 SQLite = «Si todavía no estás, espero aquí»

Si B no está disponible:

``` text
A escribe
   ↓
mensaje creado localmente
   ↓
cifrado / protegido localmente
   ↓
SQLite de A
   ↓
estado: PENDING
```

Cuando B vuelva a estar disponible y A también lo esté, A intenta
entregar la cola pendiente.

------------------------------------------------------------------------

# 3. Decisiones fundamentales

## 3.1 No existe entrega si ambos dispositivos no coinciden online

Esto es intencional.

Si A escribe mientras B está offline, el mensaje permanece en A.

Si posteriormente A queda offline y B aparece, B **no puede recibir
todavía** el mensaje porque no existe una copia en el servidor.

Solo cuando:

``` text
A = online
B = online
```

se intenta la entrega.

Esto reduce enormemente la infraestructura y evita tener buzones de
mensajes centralizados.

## 3.2 No TURN en el MVP

P2P puede fallar debido a NAT, CGNAT, firewalls o políticas de red.

Flujo inicial:

``` text
Intentar conexión directa
        |
        +-- éxito --> chat
        |
        +-- fallo --> esperar/reintentar
```

No se utilizará TURN inicialmente.

Esto evita convertir el servidor en relay de fotos, videos y archivos,
manteniendo bajos costos y reduciendo exposición.

**Importante:** debe medirse el porcentaje real de conexiones fallidas
antes de decidir permanentemente esta política.

## 3.3 El servidor no es autoridad sobre el historial

La fuente de verdad de una conversación es el almacenamiento local de
cada participante.

------------------------------------------------------------------------

# 4. Flujo de onboarding

## 4.1 Primera ejecución

1.  La aplicación genera una identidad criptográfica.
2.  La clave privada queda protegida localmente.
3.  Se crea la base de datos local cifrada.
4.  Se crea un identificador público derivado/apropiado para networking.
5.  El usuario puede escoger un nombre local de perfil si se desea.

No debería ser obligatorio crear:

-   cuenta con email;
-   número telefónico;
-   contraseña central;
-   libreta de contactos en servidor.

## 4.2 Protección de claves

La clave privada de identidad no debe guardarse como texto plano dentro
de SQLite.

Usar almacenamiento seguro del sistema operativo cuando sea posible:

-   Android Keystore;
-   iOS Keychain / Secure Enclave cuando corresponda.

El material criptográfico persistente debe protegerse siguiendo las
recomendaciones de las librerías/protocolos seleccionados.

------------------------------------------------------------------------

# 5. Agregar un contacto mediante QR

## 5.1 Contenido conceptual del QR

El formato definitivo debe versionarse, pero conceptualmente podría
contener:

``` text
protocol_version
identity_public_key
pairing_token / nonce
expiration
capabilities
signature
```

No debería contener:

``` text
private_key
permanent_shared_secret
password
historial
```

## 5.2 Emparejamiento

``` text
B muestra QR
     ↓
A escanea
     ↓
A valida estructura/firma/expiración
     ↓
se completa handshake
     ↓
ambos almacenan identidad del contacto
```

La experiencia física del QR también ayuda a reducir ataques de
suplantación.

## 5.3 Verificación humana

Como opción adicional, mostrar en ambos extremos una representación
corta derivada de las claves:

``` text
🦊 🌲 🚲 🟣
```

o palabras/números de seguridad.

Si ambos muestran lo mismo, los usuarios pueden verificar visualmente la
relación criptográfica.

------------------------------------------------------------------------

# 6. Identidad no es IP

Nunca diseñar los contactos alrededor de una IP permanente.

Un dispositivo puede pasar por:

``` text
Wi-Fi casa
   ↓
5G
   ↓
Wi-Fi oficina
   ↓
hotel
```

y obtener direcciones diferentes.

La identidad criptográfica permanece estable mientras el usuario no la
rote/reemplace.

El servidor de signaling ayuda a resolver temporalmente:

> «Sé quién eres criptográficamente. ¿Cómo intento conectarme contigo
> ahora?»

------------------------------------------------------------------------

# 7. Arquitectura de red

## 7.1 Componentes

``` text
             Signaling Service
                    |
          presencia / negociación
              /             \
             /               \
      Dispositivo A       Dispositivo B
             \               /
              \==== P2P ====/
```

Puede existir además infraestructura STUN para ayudar al establecimiento
de conexiones.

## 7.2 WebRTC DataChannel

Para un primer diseño, WebRTC DataChannel es un candidato fuerte para
transportar:

-   mensajes;
-   ACK;
-   eventos de protocolo;
-   imágenes;
-   audio;
-   archivos;
-   información de sincronización.

WebRTC/ICE se ocupa de intentar encontrar rutas válidas entre ambos
extremos.

## 7.3 STUN

STUN ayuda a un dispositivo a descubrir cómo aparece su conexión desde
Internet y participa en el proceso ICE.

STUN:

-   no debe almacenar conversaciones;
-   no necesita conocer plaintext;
-   no sustituye E2EE a nivel de aplicación.

## 7.4 TURN

TURN retransmite tráfico cuando P2P directo no es posible.

No forma parte del MVP inicialmente porque:

-   aumenta costos de ancho de banda;
-   añade infraestructura;
-   hace pasar el tráfico por un relay;
-   el producto acepta esperar cuando no exista conexión directa.

Debe quedar como decisión revisable.

------------------------------------------------------------------------

# 8. Servidor de signaling

Debe ser pequeño, stateless cuando sea razonable y con almacenamiento
mínimo.

Responsabilidades posibles:

-   mantener conexiones activas;
-   facilitar presencia;
-   permitir rendezvous entre dispositivos;
-   intercambiar mensajes de signaling/ICE;
-   aplicar rate limiting;
-   prevenir abuso básico;
-   gestionar tokens efímeros;
-   versionar el protocolo.

No debe:

-   almacenar historial de conversaciones;
-   recibir claves privadas;
-   descifrar mensajes;
-   mantener copias permanentes de fotos/archivos;
-   convertirse accidentalmente en una libreta social central.

## 8.1 WebSocket

Una opción natural es mantener una conexión WebSocket mientras la app
está activa/conectada.

Conceptualmente:

``` text
Client --> CONNECT --> Signaling
Client <-- presence/signaling events
```

Hay que considerar las restricciones de background de Android/iOS: una
app móvil no puede asumir que mantendrá indefinidamente un socket activo
en segundo plano.

Esto afecta una promesa como «enviar inmediatamente en cuanto B
aparezca» si A está suspendido por el SO.

Debe diseñarse explícitamente la estrategia de foreground/background y,
si en el futuro se usan push notifications, estudiar cuidadosamente qué
metadata se entrega a FCM/APNs.

------------------------------------------------------------------------

# 9. Presencia y privacidad

Una implementación ingenua sería:

``` text
A -> server: "¿B está conectado?"
```

Eso permite al servidor aprender relaciones sociales.

El MVP puede comenzar de manera simple, pero debe existir un objetivo
arquitectónico de minimizar metadata.

## Riesgos de metadata

Aunque el servidor no vea el mensaje, podría inferir:

-   qué identidades se conectan;
-   cuándo;
-   desde qué IP;
-   cuánto duran las sesiones;
-   qué dispositivos intentan encontrarse;
-   correlaciones temporales entre usuarios.

Por tanto:

> E2EE protege contenido; no elimina automáticamente metadata.

## Evolución futura

Investigar mecanismos de rendezvous/presencia con menor revelación de
relaciones, tokens rotatorios o identificadores efímeros.

No inventar criptografía propia para resolverlo: utilizar diseños
revisados públicamente cuando llegue el momento.

------------------------------------------------------------------------

# 10. E2EE

## 10.1 Regla principal

Nunca implementar primitivas criptográficas propias.

Utilizar librerías maduras y protocolos ampliamente analizados.

## 10.2 Objetivos

El sistema debería proporcionar:

-   confidencialidad;
-   autenticidad;
-   integridad;
-   protección contra replay;
-   forward secrecy cuando el protocolo seleccionado lo permita;
-   recuperación razonable ante pérdida/reordenamiento;
-   rotación de claves.

## 10.3 Handshake

La identidad a largo plazo no debería convertirse simplemente en «una
clave AES permanente para siempre».

Debe existir un protocolo de establecimiento de sesión apropiado.

Tecnologías/conceptos a evaluar:

-   X25519;
-   HKDF;
-   AEAD;
-   ChaCha20-Poly1305 o AES-GCM según plataforma/librería;
-   Double Ratchet / protocolos maduros equivalentes para mensajería.

La selección definitiva requiere threat model y revisión de seguridad.

## 10.4 Nonces

Nunca reutilizar un nonce con una misma clave en algoritmos donde ello
rompa la seguridad.

La generación/gestión debe delegarse en abstracciones criptográficas
seguras cuando sea posible.

------------------------------------------------------------------------

# 11. Protocolo de mensajes

Cada mensaje debe tener un identificador único.

Ejemplo conceptual:

``` text
MessageEnvelope
- protocolVersion
- conversationId / routing context
- messageId
- senderDeviceId
- sequence / ratchet metadata
- createdAt
- type
- encryptedPayload
- authentication data
```

Evitar enviar metadata innecesaria.

## 11.1 Estados locales

``` text
DRAFT
PENDING
SENDING
SENT
DELIVERED
FAILED
```

Opcionalmente, `READ` si el producto decide soportar confirmaciones de
lectura.

`READ` debe poder desactivarse si la filosofía de privacidad lo
requiere.

## 11.2 ACK

Nunca asumir que «escribí bytes al socket» significa «el destinatario
guardó el mensaje».

Flujo:

``` text
A --> message #123 --> B
B valida
B descifra/autentica
B persiste localmente
B --> ACK #123 --> A
A marca DELIVERED
```

Idealmente el ACK se envía después de persistencia exitosa.

## 11.3 Idempotencia

Caso crítico:

``` text
A -> #123 -> B
B guarda #123
B -> ACK
      X conexión cae
A reintenta #123
```

B debe detectar que `#123` ya existe y no duplicarlo.

Después vuelve a enviar ACK.

Por tanto:

> la recepción de mensajes debe ser idempotente.

## 11.4 Orden

No confiar únicamente en timestamps de reloj de pared.

Los relojes de dispositivos pueden diferir.

El protocolo debe definir cómo ordenar mensajes y cómo manejar mensajes
fuera de orden.

------------------------------------------------------------------------

# 12. Cola offline local

Cuando el usuario pulsa Enviar:

1.  Crear `messageId`.
2.  Crear el registro local.
3.  Persistirlo.
4.  Mostrarlo inmediatamente en UI.
5.  Si el peer está conectado, intentar entrega.
6.  Si no, mantener `PENDING`.
7.  Cuando se detecte conectividad válida, ejecutar el worker de outbox.
8.  Reintentar con backoff.
9.  Al recibir ACK, marcar `DELIVERED`.

Este patrón se parece a un **Transactional Outbox local**.

## Reintentos

Utilizar:

-   exponential backoff;
-   jitter;
-   límites razonables;
-   cancelación;
-   reacción inmediata a eventos de reconexión.

Evitar loops agresivos que consuman batería/red.

------------------------------------------------------------------------

# 13. SQLite local

SQLite puede contener:

``` text
contacts
devices
conversations
messages
message_delivery_state
cryptographic_session_state
attachments
outbox
settings
```

La estructura exacta debe evolucionar mediante migraciones versionadas.

## Reglas

-   claves foráneas donde tenga sentido;
-   índices en consultas frecuentes;
-   transacciones;
-   migraciones automáticas y testeadas;
-   nunca borrar datos críticos antes del ACK adecuado;
-   integridad ante cierre abrupto;
-   evitar guardar plaintext sensible innecesario.

## Cifrado local

Evaluar una solución madura de cifrado de base de datos o cifrado de
campos sensibles.

La clave de protección no debe residir junto a la DB en plaintext.

------------------------------------------------------------------------

# 14. Adjuntos

Fotos, videos y archivos deben seguir el mismo principio local-first.

Flujo:

``` text
A selecciona archivo
      ↓
se prepara/cifra
      ↓
metadata local
      ↓
PENDING
      ↓
B online
      ↓
transferencia P2P por chunks
      ↓
verificación de integridad
      ↓
ACK
```

## Chunks

Para archivos grandes:

-   dividir en fragmentos;
-   permitir reanudación;
-   verificar integridad;
-   aplicar límites de tamaño configurables;
-   no cargar todo el archivo en RAM.

Debe existir protección contra:

-   archivos gigantes maliciosos;
-   zip bombs;
-   nombres/path traversal;
-   formatos inesperados;
-   agotamiento de disco.

------------------------------------------------------------------------

# 15. Arquitectura del cliente

Se recomienda combinar **Clean Architecture + MVVM**.

``` text
┌──────────────────────────────┐
│ Presentation                 │
│ Views / ViewModels           │
├──────────────────────────────┤
│ Domain                       │
│ Entities / Use Cases         │
├──────────────────────────────┤
│ Data                         │
│ Repositories / Data Sources  │
├──────────────────────────────┤
│ Infrastructure               │
│ DB / Crypto / Network / OS   │
└──────────────────────────────┘
```

La dependencia debe apuntar hacia el dominio, no al revés.

------------------------------------------------------------------------

# 16. MVVM

## View

Responsable de:

-   renderizar;
-   capturar interacción;
-   observar estado.

No debe:

-   ejecutar SQL;
-   realizar criptografía;
-   manejar sockets directamente;
-   contener reglas de negocio.

## ViewModel

Responsable de:

-   exponer `UiState`;
-   recibir intents/actions;
-   invocar Use Cases;
-   transformar resultados de dominio a estado de UI.

Ejemplo conceptual:

``` text
ChatView
   ↓ send("Hola")
ChatViewModel
   ↓
SendMessageUseCase
```

El ViewModel no debería saber cómo funciona WebRTC internamente.

## Model / Domain

Contiene conceptos como:

``` text
Contact
Conversation
Message
MessageStatus
DeviceIdentity
Attachment
```

------------------------------------------------------------------------

# 17. Clean Architecture

## Domain

Debe ser lo más independiente posible de frameworks.

### Entities

Ejemplos:

``` text
Message
Conversation
Contact
PeerIdentity
Attachment
DeliveryState
```

### Use Cases

Ejemplos:

``` text
CreateIdentity
PairContact
SendMessage
RetryPendingMessages
ReceiveMessage
AcknowledgeMessage
ConnectToPeer
DisconnectPeer
DeleteConversation
VerifySafetyCode
```

## Repository interfaces

El dominio depende de contratos:

``` text
MessageRepository
ContactRepository
PeerConnectionRepository
CryptoRepository
IdentityRepository
AttachmentRepository
```

No de implementaciones concretas.

## Data/Infrastructure

Implementaciones:

``` text
SqliteMessageRepository
WebRtcPeerConnectionRepository
SecureCryptoRepository
WebSocketSignalingRepository
KeystoreIdentityRepository
```

Esto permite reemplazar tecnologías sin reescribir el dominio.

------------------------------------------------------------------------

# 18. SOLID y Clean Code

## Single Responsibility

Una clase/componente debe tener un motivo principal de cambio.

No crear:

``` text
ChatManager
```

que haga simultáneamente:

-   SQL;
-   WebRTC;
-   cifrado;
-   UI;
-   archivos;
-   logging.

## Dependency Inversion

Preferir:

``` text
SendMessageUseCase
        |
MessageRepository interface
        |
SqliteMessageRepository
```

en lugar de que el Use Case conozca SQLite.

## Interfaces pequeñas

Evitar interfaces enormes tipo `EverythingRepository`.

## Nombres

Preferir nombres que expresen intención:

``` text
retryPendingMessages()
markMessageDelivered()
verifyPeerIdentity()
```

sobre:

``` text
process()
handle()
doStuff()
```

cuando no expresan contexto.

## Funciones

-   pequeñas;
-   predecibles;
-   con efectos secundarios explícitos;
-   errores tipados cuando sea posible.

## Estado

Evitar estado global mutable.

------------------------------------------------------------------------

# 19. Concurrencia

Mensajería + networking + SQLite implica carreras potenciales.

Ejemplos:

-   llega ACK mientras UI borra conversación;
-   dos reconexiones disparan dos workers;
-   se recibe dos veces el mismo mensaje;
-   cambia la sesión criptográfica durante envío.

Diseñar:

-   operaciones idempotentes;
-   transacciones;
-   serialización por conversación/sesión cuando sea necesaria;
-   ownership claro del estado;
-   structured concurrency;
-   cancelación.

------------------------------------------------------------------------

# 20. State machines

Las conexiones deberían modelarse explícitamente.

Ejemplo:

``` text
DISCONNECTED
    ↓
DISCOVERING
    ↓
NEGOTIATING
    ↓
CONNECTED
    ↓
RECONNECTING
    ↓
DISCONNECTED
```

Evitar docenas de booleanos:

``` text
isConnected
isConnecting
isRetrying
hasPeer
...
```

que puedan formar estados imposibles.

Lo mismo aplica a transferencias y mensajes.

------------------------------------------------------------------------

# 21. Background y ciclo de vida móvil

Este es un punto crítico.

Android/iOS pueden suspender procesos y conexiones.

La app debe soportar:

-   proceso destruido;
-   app suspendida;
-   pérdida de Wi-Fi;
-   cambio Wi-Fi → celular;
-   cambio de IP;
-   reinicio del teléfono;
-   batería baja;
-   modo ahorro;
-   permisos revocados.

Al volver:

``` text
restaurar estado local
   ↓
reconectar signaling
   ↓
reconstruir presencia
   ↓
intentar sesiones necesarias
   ↓
procesar outbox
```

Nunca depender de memoria RAM para estado crítico.

------------------------------------------------------------------------

# 22. Multi-dispositivo

No es obligatorio para MVP, pero debe evitarse una arquitectura que lo
haga imposible.

Modelo futuro:

``` text
Identidad del usuario
       |
   +---+---+
   |       |
 móvil     PC
```

El dispositivo existente puede autorizar otro mediante QR.

Cada dispositivo debería tener identidad/clave propia dentro de la
identidad lógica del usuario.

Debe existir:

-   autorización;
-   lista local/sincronizable de dispositivos;
-   revocación;
-   rotación;
-   notificación de dispositivo nuevo;
-   estrategia de sincronización E2EE.

No compartir simplemente una única clave privada maestra copiada sin
control entre todos los dispositivos.

------------------------------------------------------------------------

# 23. Cambio o pérdida de teléfono

Debe definirse desde producto.

Si el servidor no tiene claves ni historial, entonces perder el teléfono
puede significar perder:

-   identidad;
-   contactos;
-   historial.

Opciones futuras:

1.  **Sin recuperación:** máxima simplicidad/privacidad.
2.  **Backup local exportable cifrado.**
3.  **Backup cloud cifrado client-side**, donde el proveedor nunca posea
    la clave de descifrado.

No prometer recuperación que contradiga el modelo criptográfico.

------------------------------------------------------------------------

# 24. Borrado

Distinguir:

### Borrar para mí

Elimina datos locales.

### Solicitar borrar en ambos extremos

Puede enviarse un evento autenticado solicitando eliminación.

Pero debe comunicarse correctamente:

> No existe forma técnica de garantizar que el destinatario no haya
> hecho una captura, exportado el contenido o modificado su cliente.

No vender «borrado remoto garantizado».

------------------------------------------------------------------------

# 25. Bloqueo y abuso

Incluso sin cuentas tradicionales se necesita protección.

Funciones:

-   bloquear identidad;
-   rechazar conexiones futuras;
-   rate limiting;
-   límites de tamaño;
-   límites de intentos de pairing;
-   expiración de invitaciones;
-   protección contra QR maliciosos;
-   validación estricta de paquetes;
-   límites de memoria;
-   timeouts.

Una arquitectura privada no debe convertirse en una arquitectura fácil
de DoS.

------------------------------------------------------------------------

# 26. Threat model

Antes de afirmar que la aplicación es «segura», documentar contra quién
protege y contra quién no.

## Debe intentar proteger contra

-   servidor curioso;
-   interceptación de red;
-   Wi-Fi hostil;
-   modificación de mensajes en tránsito;
-   replay;
-   suplantación no verificada;
-   filtración accidental desde logs;
-   robo de DB sin desbloqueo de claves, dentro de las capacidades de la
    plataforma.

## No puede garantizar protección contra

-   dispositivo desbloqueado y comprometido;
-   malware con control total del endpoint;
-   destinatario haciendo screenshots;
-   usuario exportando contenido;
-   vulnerabilidades desconocidas del SO/hardware.

La seguridad de extremo a extremo termina en los extremos.

------------------------------------------------------------------------

# 27. Logging

**Nunca loggear:**

-   plaintext de mensajes;
-   claves;
-   secretos;
-   tokens completos;
-   contenido de archivos;
-   QR completos sensibles.

Logs de producción deben ser mínimos.

Ejemplo aceptable:

``` text
peer_connection_failed
reason=NAT_NEGOTIATION_TIMEOUT
protocolVersion=3
```

en lugar de incluir identificadores permanentes innecesarios.

------------------------------------------------------------------------

# 28. Analytics y crash reporting

Evitar SDKs invasivos.

Si se usa telemetría:

-   opt-in cuando corresponda;
-   datos mínimos;
-   identificadores rotatorios/anonimizados cuando sean realmente
    apropiados;
-   nunca contenido;
-   revisar cuidadosamente crash dumps;
-   no adjuntar DB automáticamente.

Una app que promete privacidad pierde credibilidad si incorpora
múltiples trackers publicitarios.

------------------------------------------------------------------------

# 29. Seguridad del servidor

Aunque no tenga mensajes, sigue siendo infraestructura sensible.

Aplicar:

-   TLS;
-   autenticación de protocolo;
-   rate limiting;
-   límites de payload;
-   timeouts;
-   validación estricta;
-   protección DoS;
-   actualizaciones;
-   secrets management;
-   mínimo privilegio;
-   firewall;
-   observabilidad;
-   backups solo de lo realmente necesario;
-   retención mínima de logs;
-   separación dev/staging/prod.

No confiar en input del cliente.

------------------------------------------------------------------------

# 30. Versionado del protocolo

Desde el principio:

``` text
protocolVersion: 1
```

Los mensajes/envelopes deben ser evolutivos.

Considerar:

-   compatibilidad;
-   campos desconocidos;
-   feature negotiation;
-   capacidades;
-   deprecación;
-   actualización mínima requerida.

Evitar que una actualización de app rompa todos los peers antiguos sin
estrategia.

------------------------------------------------------------------------

# 31. Formatos y serialización

Elegir un formato claramente especificado.

Opciones a evaluar:

-   Protocol Buffers;
-   CBOR;
-   otro formato binario bien definido.

JSON puede ser útil inicialmente para signaling/debug, pero no debe
elegirse automáticamente para todo.

Definir límites máximos antes de parsear.

------------------------------------------------------------------------

# 32. Separar signaling de messaging

Nunca mezclar ambos conceptos.

``` text
SignalingChannel
- presencia
- negociación
- ICE

SecureMessageChannel
- mensajes E2EE
- ACK
- archivos
```

Esto facilita seguridad, pruebas y reemplazo de tecnologías.

------------------------------------------------------------------------

# 33. Descubrimiento LAN

Función futura interesante.

Si ambos dispositivos están en la misma red local:

``` text
A <==== LAN ====> B
```

podrían descubrirse/conectarse sin depender de Internet.

Debe realizarse de forma que no anuncie innecesariamente identidad
estable a todos los dispositivos de la red.

------------------------------------------------------------------------

# 34. Wi-Fi Direct / Bluetooth

Posible evolución para comunicación sin Internet.

No incluir en MVP.

La arquitectura de transporte debería permitir en el futuro:

``` text
PeerTransport
├── WebRtcTransport
├── LanTransport
├── WifiDirectTransport
└── BluetoothTransport
```

El dominio no debería saber cuál se está usando.

------------------------------------------------------------------------

# 35. Interfaces importantes

Ejemplo conceptual:

``` text
interface PeerTransport {
    connect(peer)
    disconnect(peer)
    send(packet)
    observeState()
}

interface CryptoService {
    establishSession(peer)
    encrypt(session, plaintext)
    decrypt(session, ciphertext)
}

interface MessageRepository {
    insert(message)
    pending(conversation)
    markDelivered(messageId)
}
```

Los nombres/lenguajes concretos dependerán del stack.

------------------------------------------------------------------------

# 36. Estructura de módulos sugerida

``` text
app/
presentation/
    chat/
    contacts/
    pairing/
    settings/

domain/
    model/
    usecase/
    repository/

data/
    repository/
    local/
    mapper/

network/
    signaling/
    p2p/
    protocol/

security/
    identity/
    crypto/
    keystore/

storage/
    database/
    migration/
    attachments/

core/
    errors/
    logging/
    concurrency/
```

Evitar un `utils/` gigantesco que termine siendo un cajón de sastre.

------------------------------------------------------------------------

# 37. Errores

Definir errores de dominio.

Ejemplos:

``` text
PeerUnavailable
PeerConnectionFailed
IdentityVerificationFailed
MessageAuthenticationFailed
StorageFull
AttachmentTooLarge
ProtocolVersionUnsupported
SessionExpired
```

La UI traduce esos errores a mensajes entendibles.

No mostrar stack traces al usuario.

------------------------------------------------------------------------

# 38. UX de estados

El usuario debe entender qué sucede.

Ejemplo:

``` text
🕒 pendiente localmente
↑ enviando
✓ entregado
! no se pudo conectar
```

No afirmar «enviado» cuando solamente está almacenado en SQLite del
emisor.

Diferenciar:

-   creado;
-   pendiente;
-   transmitido;
-   confirmado.

------------------------------------------------------------------------

# 39. Pruebas

## Unit tests

Especialmente:

-   Use Cases;
-   state machines;
-   reintentos;
-   deduplicación;
-   orden;
-   serialización;
-   validaciones.

## Integration tests

-   SQLite real;
-   migraciones;
-   repositorios;
-   crypto wrapper;
-   signaling;
-   WebRTC.

## End-to-end

Simular:

-   A/B online;
-   B offline;
-   A manda 100 mensajes;
-   reconexión;
-   pérdida del ACK;
-   paquetes duplicados;
-   paquetes desordenados;
-   cambio de red;
-   app asesinada;
-   teléfono reiniciado;
-   archivo interrumpido;
-   DB llena;
-   clock skew.

## Property/fuzz testing

Especialmente valioso para parsers y protocolo.

Inputs de red deben considerarse hostiles.

------------------------------------------------------------------------

# 40. CI/CD

Pipeline mínimo:

``` text
format
lint
unit tests
integration tests
security/static analysis
build
```

Además:

-   dependencias fijadas;
-   actualización controlada;
-   revisión de vulnerabilidades;
-   builds reproducibles como objetivo;
-   firma segura de releases;
-   secretos fuera del repositorio.

------------------------------------------------------------------------

# 41. Git y desarrollo

Recomendaciones:

-   ramas pequeñas;
-   PRs revisables;
-   Conventional Commits opcional;
-   ADRs para decisiones arquitectónicas;
-   CODEOWNERS para crypto/security si el equipo crece;
-   ninguna clave/secreto en Git.

Crear:

``` text
/docs/adr/
```

Ejemplos:

``` text
ADR-001-local-first.md
ADR-002-no-turn-mvp.md
ADR-003-webrtc-datachannel.md
ADR-004-identity-model.md
```

------------------------------------------------------------------------

# 42. Documentación del protocolo

Mantener una especificación independiente de la implementación.

Debe explicar:

-   identidad;
-   pairing;
-   handshake;
-   envelopes;
-   estados;
-   ACK;
-   reintentos;
-   deduplicación;
-   archivos;
-   errores;
-   versionado;
-   seguridad;
-   threat model.

Así, en el futuro podrían existir clientes compatibles escritos en
tecnologías diferentes.

------------------------------------------------------------------------

# 43. Open source

Una aplicación centrada en privacidad se beneficia de:

-   cliente auditable;
-   protocolo público;
-   builds verificables como meta;
-   auditorías independientes cuando el proyecto madure.

«Confía en nosotros» es más débil que «puedes verificar el diseño y la
implementación».

------------------------------------------------------------------------

# 44. Privacidad por diseño

Minimizar:

``` text
datos recolectados
retención
logs
identificadores
dependencias de terceros
metadata
```

Antes de añadir cualquier campo al backend preguntar:

> ¿Por qué necesitamos saber esto?

Y:

> ¿Durante cuánto tiempo necesitamos conservarlo?

------------------------------------------------------------------------

# 45. Consideraciones legales/producto

El hecho de no almacenar mensajes no significa que el servicio no
procese ningún dato.

Dependiendo de jurisdicción, pueden ser relevantes:

-   IP;
-   logs;
-   identificadores de dispositivo;
-   tokens;
-   telemetría;
-   reportes de abuso.

Antes de producción pública se requiere revisión legal apropiada para
los países donde opere.

La política de privacidad debe describir el sistema real, no una versión
idealizada.

------------------------------------------------------------------------

# 46. MVP recomendado

## Fase 1 --- Fundamentos

-   identidad local;
-   almacenamiento seguro;
-   SQLite;
-   modelos de dominio;
-   Clean Architecture;
-   MVVM;
-   migraciones.

## Fase 2 --- Pairing

-   QR;
-   validación;
-   contacto local;
-   safety code.

## Fase 3 --- Signaling

-   WebSocket;
-   presencia;
-   protocolo versionado;
-   reconexión.

## Fase 4 --- P2P

-   WebRTC DataChannel;
-   ICE/STUN;
-   conexión directa;
-   state machine.

## Fase 5 --- Mensajes

-   E2EE;
-   outbox;
-   ACK;
-   idempotencia;
-   reintentos;
-   estados UI.

## Fase 6 --- Robustez

-   background/foreground;
-   reinicios;
-   cambio de red;
-   tests E2E;
-   métricas técnicas respetuosas de privacidad.

## Fase 7 --- Adjuntos

-   chunks;
-   reanudación;
-   cifrado;
-   integridad;
-   límites.

------------------------------------------------------------------------

# 47. Lo que NO debe hacerse en el MVP

Evitar inicialmente:

-   grupos;
-   llamadas;
-   historias/status;
-   bots;
-   backups cloud;
-   sincronización multi-device completa;
-   TURN propio;
-   Bluetooth;
-   Wi-Fi Direct;
-   federación;
-   blockchain;
-   criptografía inventada;
-   sistema complejo de usernames globales.

Primero demostrar que el núcleo funciona bien.

------------------------------------------------------------------------

# 48. Métricas técnicas importantes

Sin registrar contenido, necesitamos conocer:

-   porcentaje de conexiones P2P exitosas;
-   tiempo mediano para establecer conexión;
-   causas agregadas de fallo ICE;
-   reconexiones;
-   tasa de mensajes finalmente entregados;
-   latencia de ACK;
-   crashes;
-   consumo de batería;
-   transferencia de archivos interrumpida.

Estas métricas deben diseñarse con privacidad desde el inicio y
minimizar identificadores persistentes.

La métrica más importante para decidir TURN:

> **¿Qué porcentaje de pares reales no consigue establecer P2P
> directo?**

------------------------------------------------------------------------

# 49. Escalabilidad del signaling

El servidor será barato inicialmente porque no transporta el contenido
pesado.

Pero hay que diseñar para:

-   muchas conexiones WebSocket concurrentes;
-   heartbeats razonables;
-   expiración de presencia;
-   reconexión con jitter;
-   evitar thundering herd;
-   balanceo futuro;
-   pub/sub o coordinación entre nodos si se horizontaliza.

No optimizar prematuramente, pero tampoco acoplar todo a memoria de un
único proceso sin abstracción.

------------------------------------------------------------------------

# 50. Disponibilidad y fallo del servidor

Aunque no almacene mensajes, si signaling cae:

``` text
nuevas conexiones P2P por Internet pueden no encontrarse
```

Las conversaciones locales siguen existiendo.

El cliente debe:

-   mostrar estado comprensible;
-   reintentar con backoff;
-   conservar outbox;
-   no perder mensajes;
-   recuperar automáticamente al volver el servicio.

Un futuro modo LAN puede seguir funcionando independientemente.

------------------------------------------------------------------------

# 51. DNS y endpoint bootstrap

Los clientes necesitan saber cómo localizar signaling/STUN.

No hardcodear de forma imposible de migrar.

Diseñar:

-   configuración/versionado;
-   migración de endpoints;
-   certificate validation;
-   protección contra downgrade.

------------------------------------------------------------------------

# 52. Actualizaciones de seguridad

Debe existir capacidad para:

-   bloquear versiones críticamente vulnerables;
-   anunciar protocolo mínimo;
-   rotar infraestructura;
-   migrar formatos;
-   revocar material comprometido cuando el diseño lo permita.

La privacidad no sirve si un cliente vulnerable permanece interoperando
indefinidamente.

------------------------------------------------------------------------

# 53. Spam e invitaciones

El QR físico reduce spam, pero las invitaciones remotas pueden
introducirlo.

Invitaciones:

-   de un solo uso;
-   expirables;
-   con entropía suficiente;
-   revocables cuando sea posible;
-   rate-limited.

No crear un directorio público global en el MVP.

------------------------------------------------------------------------

# 54. Grupos --- consideración futura

Los grupos complican mucho:

-   distribución de claves;
-   miembros que entran/salen;
-   mensajes offline;
-   sincronización;
-   múltiples conexiones P2P;
-   metadata.

No tratarlos como «un chat individual con más usuarios».

Requieren diseño específico.

------------------------------------------------------------------------

# 55. Backups

Un backup inseguro puede destruir las garantías de E2EE.

Si algún día existen:

``` text
historial
   ↓
cifrado en dispositivo
   ↓
backup
```

El servidor/cloud no debería recibir plaintext ni claves capaces de
descifrarlo.

La recuperación de clave debe diseñarse cuidadosamente para no crear una
puerta trasera involuntaria.

------------------------------------------------------------------------

# 56. Capturas y exportación

La aplicación puede intentar restringir screenshots en ciertas
plataformas/modos, pero no debe prometer imposibilidad de copia.

El destinatario siempre puede:

-   usar otro teléfono para fotografiar;
-   modificar su cliente;
-   exportar manualmente.

La seguridad protege transporte y almacenamiento controlado, no puede
controlar físicamente al destinatario.

------------------------------------------------------------------------

# 57. Principios de UX de seguridad

Evitar mensajes técnicos como:

``` text
ICE candidate gathering failed
```

Preferir:

``` text
No pudimos establecer una conexión directa con este contacto.
Tu mensaje seguirá pendiente en este dispositivo.
```

Pero permitir una pantalla de diagnóstico avanzada para
desarrolladores/usuarios técnicos sin exponer secretos.

------------------------------------------------------------------------

# 58. Auditoría de seguridad antes de producción

Antes de promocionar el producto como seguro:

1.  threat model formal;
2.  revisión criptográfica;
3.  revisión de protocolo;
4.  pentest;
5.  auditoría del cliente;
6.  auditoría del backend;
7.  revisión de dependencias;
8.  análisis de metadata;
9.  revisión de almacenamiento local;
10. pruebas de pérdida/compromiso de dispositivos.

Evitar claims absolutos como «imposible de hackear».

------------------------------------------------------------------------

# 59. Definición del núcleo del producto

La primera versión verdaderamente útil puede reducirse a:

``` text
GENERAR IDENTIDAD
        ↓
AGREGAR POR QR
        ↓
VER PRESENCIA
        ↓
ESCRIBIR MENSAJE
        ↓
GUARDAR LOCALMENTE
        ↓
¿PEER DISPONIBLE?
   /           \
 NO             SÍ
 |               |
PENDING      CONECTAR P2P
                 |
              E2EE SEND
                 |
                ACK
                 |
             DELIVERED
```

Si esto es sólido, el resto puede crecer alrededor.

------------------------------------------------------------------------

# 60. Revisión final de cobertura

Después de revisar nuevamente la arquitectura, estos son los puntos que
no deben quedar implícitos:

### Identidad y claves

-   [x] identidad local;
-   [x] claves privadas nunca en servidor;
-   [x] QR sin secretos permanentes;
-   [x] verificación de identidad;
-   [x] futura rotación/revocación;
-   [x] pérdida de dispositivo contemplada.

### Networking

-   [x] IP dinámica contemplada;
-   [x] signaling;
-   [x] WebSocket;
-   [x] WebRTC DataChannel;
-   [x] ICE/STUN;
-   [x] TURN excluido inicialmente de forma consciente;
-   [x] NAT/CGNAT/firewalls;
-   [x] reconexión;
-   [x] cambio de red;
-   [x] caída del signaling;
-   [x] restricciones móviles en background.

### Mensajería

-   [x] outbox local;
-   [x] envío cuando ambos coinciden online;
-   [x] ACK;
-   [x] idempotencia;
-   [x] deduplicación;
-   [x] reintentos;
-   [x] estados;
-   [x] orden/relojes;
-   [x] adjuntos y reanudación.

### Seguridad

-   [x] E2EE;
-   [x] no inventar criptografía;
-   [x] AEAD;
-   [x] forward secrecy como objetivo;
-   [x] replay;
-   [x] almacenamiento local cifrado;
-   [x] Keystore/Keychain;
-   [x] logging seguro;
-   [x] threat model;
-   [x] validación de input;
-   [x] DoS/rate limits;
-   [x] auditoría antes de claims fuertes.

### Arquitectura de software

-   [x] MVVM;
-   [x] Clean Architecture;
-   [x] SOLID;
-   [x] Dependency Inversion;
-   [x] repositories;
-   [x] use cases;
-   [x] state machines;
-   [x] concurrencia;
-   [x] migraciones;
-   [x] testing;
-   [x] CI/CD;
-   [x] ADRs;
-   [x] versionado del protocolo.

### Privacidad

-   [x] servidor sin historial;
-   [x] metadata reconocida como riesgo;
-   [x] minimización;
-   [x] analytics limitado;
-   [x] presencia privada como evolución;
-   [x] privacidad por diseño;
-   [x] consideraciones legales.

### Evolución

-   [x] multi-dispositivo;
-   [x] backup E2EE;
-   [x] LAN;
-   [x] Wi-Fi Direct/Bluetooth;
-   [x] grupos reconocidos como problema separado;
-   [x] protocolo público/open source;
-   [x] escalamiento del signaling.

------------------------------------------------------------------------

# 61. Regla de arquitectura

Toda nueva funcionalidad debería responder estas preguntas antes de
implementarse:

1.  **¿Puede hacerse localmente?**
2.  **¿Qué datos nuevos conocería el servidor?**
3.  **¿Es estrictamente necesario que los conozca?**
4.  **¿Durante cuánto tiempo?**
5.  **¿Qué ocurre si el servidor es comprometido?**
6.  **¿Qué ocurre si un dispositivo es comprometido?**
7.  **¿Qué metadata crea esta funcionalidad?**
8.  **¿Rompe E2EE o forward secrecy?**
9.  **¿Funciona después de reiniciar/matar la app?**
10. **¿Cómo se prueba?**
11. **¿Cómo se migra/versiona?**
12. **¿Cómo falla sin perder mensajes?**

Si una funcionalidad requiere debilitar las garantías fundamentales,
debe tratarse como una decisión explícita de arquitectura, no como un
detalle de implementación.

------------------------------------------------------------------------

# 62. Resumen arquitectónico

``` text
┌─────────────────────────────────────────────────────────┐
│                       DISPOSITIVO A                     │
│                                                         │
│  UI -> ViewModel -> Use Cases -> Repositories           │
│                              |                          │
│              +---------------+---------------+          │
│              |               |               |          │
│          SQLite          Crypto          P2P/WebRTC     │
│          cifrado         + Keystore           |         │
└───────────────────────────────────────────────|─────────┘
                                                |
                                      E2EE DataChannel
                                                |
┌───────────────────────────────────────────────|─────────┐
│                       DISPOSITIVO B                     │
│                                                         │
│  P2P/WebRTC      Crypto + Keystore       SQLite cifrado │
└─────────────────────────────────────────────────────────┘

                    ^                 ^
                    |                 |
                    +---- SIGNALING --+
                           |
                    presencia/rendezvous
                    sin historial de chat
```

## Filosofía final

El objetivo no es afirmar que «no existen servidores».

El objetivo más útil y verificable es:

> **Los servidores no deben necesitar poseer las conversaciones ni las
> claves privadas de los usuarios para que el producto funcione.**

Y, siempre que sea viable:

> **Los datos del usuario viven con el usuario.**
