# Memoria del proyecto — The Lights Before Dawn

Este archivo conserva contexto técnico útil entre tareas. No sustituye el código ni las pruebas; verificarlo si el proyecto cambió.

## Proyecto y arquitectura
- Juego 3D de exploración y sigilo en Unity 6.3/C#: un niño recorre una casa modular, se esconde de aliens y atraviesa conexiones que pueden alterarse con la anomalía.
- Escena principal: `Assets/Scenes/ModularPrototype.unity`. La prueba aislada de portales está en `Assets/Scenes/PortalTrial_ChildCorridor.unity`.
- `Core` controla sesión e historia; `Player`, movimiento y ocultamiento; `Audio`, señales sonoras; `Aliens`, estados, percepción, memoria y navegación.
- `Rooms` define módulos, puertas, ocupantes y viajeros del portal; `Anomaly/HouseFlowController` coordina conexiones, solicitudes de apertura y destinos.
- Los patrones presentes incluyen estados (`AlienState`), estrategias (`IRoomSelectionStrategy`, `ISearchStrategy`), interfaces de interacción y eventos de conexión.

## Contratos y límites que preservar
- Antes de la anomalía, las conexiones normales deben respetar la topología de la casa; después, los destinos elegibles pueden variar sin dejar al jugador o al alien atrapados.
- El portal debe unir vista y cruce físico: no mostrar el exterior ni puertas duplicadas, y mantener suelo, colisiones, orientación y continuidad de movimiento.
- Puede haber más de una puerta abierta. Tras cerrar una conexión anómala existe un margen configurado de un segundo antes de cambiar su destino, para permitir una persecución inmediata.
- La visión alien debe respetar ángulo, alcance y obstáculos de ambos lados de un portal abierto; una puerta cerrada o un muro no deben volverse transparentes para el raycast.
- Preservar `.meta`, GUID, referencias serializadas y la estructura de escenas/prefabs. No modificar assets durante Play ni confundir compilación correcta con prueba jugable.

## Riesgos y errores que no se deben repetir
- Hubo pruebas donde se veía el cielo al cruzar, aparecían dos hojas de puerta y el jugador caía al vacío. Cualquier cambio en portales exige volver a comprobar esos tres casos.
- El 2026-10-19 se corrigió la visión del alien a través de un portal: `AlienPerception` transforma el rayo en el marco, comprueba ambos tramos contra obstáculos reales y rechaza conexiones con alguna puerta cerrada; `PortalTrialGate.MapDirection` conserva la dirección. El usuario verificó en Play detección y persecución en ambos sentidos, oclusión por puerta/muro y cruce cerca del marco, sin errores de compilación.
- Las pruebas del menú `The Lights Before Dawn > Pruebas` cubren historia/puertas, visión/sonido/captura y patrulla/navegación; complementarlas con Play cuando sea posible.

## Criterio de actualización
- Añadir solo decisiones duraderas, cambios realmente aplicados, resultados comprobados y errores concretos que convenga evitar; fechar un estado temporal y retirarlo cuando quede obsoleto.
- Mantener entradas breves y específicas; no copiar chats, logs extensos ni hipótesis como si fueran hechos.
- Nunca guardar claves, tokens, credenciales, datos personales ni rutas privadas de la computadora. Usar rutas relativas al proyecto.
