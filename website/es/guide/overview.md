# Qué es Murmur

**Murmur** es una aplicación de mensajería privada con tres propiedades que normalmente no van juntas:

1. **Local-first.** Tus conversaciones viven en tus dispositivos, no en la nube de nadie.
2. **Cifrado de extremo a extremo.** Solo los dos teléfonos de la conversación pueden leer los mensajes.
3. **Peer-to-peer.** Los mensajes viajan directamente de un teléfono a otro; el servidor solo ayuda a que se encuentren.

> **Principio rector:** si una funcionalidad puede hacerse de forma segura en el dispositivo,
> necesita una buena razón para ir al servidor.

## Qué hace distinto

| Mensajería habitual | Murmur |
|---|---|
| Te registras con tu número o email | Tu identidad es un par de claves generado en tu teléfono |
| El servidor guarda los mensajes hasta que los recibes | No hay buzón: el mensaje espera **en tu teléfono** |
| El servidor sabe quién es contacto de quién | Los contactos solo existen en los dos teléfonos |
| Añades contactos por su número | Os emparejáis **en persona** escaneando un QR |
| Recuperas todo al cambiar de teléfono | Sin recuperación en el MVP: lo que no está en tu teléfono no existe |

## Las reglas del juego (decisiones de producto)

::: info Los mensajes viajan cuando los dos teléfonos coinciden
No hay buzón en el servidor ([ADR-001](/es/guide/decisions#adr-001)): mientras la otra persona no esté
conectada, el mensaje espera en tu teléfono marcado como **🕒 pendiente en este dispositivo**. No
hace falta tener la app abierta: en Android, un trabajo en segundo plano reintenta la entrega cada
15 minutos, y quien activa **Siempre disponible** puede recibir con la app cerrada
([ADR-012](/es/guide/decisions#adr-012), [más detalles](/es/guide/life-of-a-message#sin-abrir-la-app)).
:::

- **Tus contactos ven tu IP pública** mientras habláis: es el precio de la conexión directa.
- **Sin TURN**: en algunas redes (CGNAT estricto, redes corporativas) no habrá ruta directa y los
  mensajes seguirán pendientes ([ADR-002](/es/guide/decisions#adr-002)).
- **Sin recuperación**: perder el teléfono es perder identidad, contactos e historial.
- **"Borrar" significa borrar en tu teléfono.** Nadie puede garantizar el borrado en el otro.

## Qué incluye hoy

- Identidad local con claves en el Android Keystore.
- Base de datos SQLCipher con migraciones.
- Emparejamiento por QR con código de seguridad de 60 dígitos.
- Servidor de signaling con temas rotativos, presencia y límites anti-abuso.
- Sesiones Noise, outbox, ACK, idempotencia, reintentos, orden Lamport y contrapresión.
- App .NET MAUI: conversaciones, chat con estados honestos, QR, escáner, ficha de contacto, ajustes.
- Especificación del protocolo, modelo de amenazas y 11 ADRs.

El transporte WebRTC real es la [siguiente fase](/es/guide/roadmap). Mientras tanto, las compilaciones
Debug incluyen un transporte solo de desarrollo para probar en teléfonos reales.

## Lo que Murmur no es (todavía)

Grupos, llamadas, adjuntos, multi-dispositivo, backups y versión web quedan fuera del MVP a propósito.
Primero, que el núcleo funcione bien.
