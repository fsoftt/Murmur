# ADR-012 — Entrega en segundo plano y modo "siempre disponible"

**Estado:** aceptado · matiza [ADR-001](ADR-001-local-first.md)

## Contexto
Con ADR-001 un mensaje solo se entrega si ambos tienen la app abierta a la vez, porque Android e
iOS suspenden las apps en segundo plano. Es la mayor fricción del producto. Queremos reducirla sin
guardar mensajes en ningún servidor.

## Decisión
- **A. Entrega en segundo plano (Android, WorkManager).** Un trabajo periódico (cada 15 min, el
  mínimo de Android, solo con red) y uno inmediato al salir de la app arrancan el cliente e intentan
  vaciar el outbox durante unos minutos (`MurmurClient.DeliverPendingAsync`). El emisor ya no
  necesita abrir la app.
- **B. Modo "siempre disponible" (opcional).** Un servicio en primer plano de tipo
  `remoteMessaging`, con su notificación permanente obligatoria, mantiene el cliente conectado al
  signaling con la app cerrada, así el teléfono puede **recibir**. Se reactiva tras reiniciar el
  teléfono. La app avisa si la optimización de batería puede pausarlo.
- Los mensajes recibidos con la app cerrada generan una notificación **sin el texto** del mensaje
  (solo "Nuevo mensaje" y el nombre local del contacto).
- Nada cambia en el servidor ni en el protocolo: sigue sin guardar nada.

## Consecuencias
- Con A en el emisor y B en el receptor, la entrega ya no depende de abrir la app a la vez.
- B consume algo más de batería y muestra un aviso permanente; por eso es opcional.
- Si el receptor no tiene B y no está usando la app, el mensaje sigue esperando en el emisor.
- iOS no permite ni A ni B de forma equivalente; necesitará un aviso push sin contenido (decisión aparte).
- Android 12+ solo permite iniciar el servicio con la app visible o al arrancar el teléfono; en otro
  caso se reanuda la próxima vez que se abra la app.
