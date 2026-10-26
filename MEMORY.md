# Memoria del proyecto — The Lights Before Dawn

Este archivo conserva contexto técnico útil entre tareas. No sustituye el código ni las pruebas; verificarlo si el proyecto cambió.

## Proyecto y arquitectura
- Juego 3D de exploración y sigilo en Unity 6.3/C#: un niño recorre una casa modular, se esconde de aliens y atraviesa conexiones que pueden alterarse con la anomalía.
- Escena principal: `Assets/Scenes/ModularPrototype.unity`. La prueba aislada de portales está en `Assets/Scenes/PortalTrial_ChildCorridor.unity`.
- `Core` controla sesión e historia; `Player`, movimiento y ocultamiento; `Audio`, señales sonoras; `Aliens`, estados, percepción, memoria y navegación.
- `Rooms` define módulos, puertas, ocupantes y viajeros del portal; `Anomaly/HouseFlowController` coordina conexiones, solicitudes de apertura y destinos.
- El rol `blender_modeler` está configurado en `.codex/agents` para producir assets 3D desde referencias, con fuente `.blend`, exportación y verificación visual; no modifica escenas de Unity por defecto.
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
- El 2026-10-20 se integraron modelos Poly Haven en `ModularPrototype`: libros en las cinco estanterías, laptops en niño/oficina, microondas nuevo sobre la mesada y cocina FBX en lugar de la visual ProBuilder. La cocina conserva los colliders greybox; la heladera quedó intacta. Los recursos están en `Assets/Props/PolyHaven` y el menú de editor `Arte > Integrar libros y electrodomésticos Poly Haven` evita duplicar la colocación. La escena se guardó y la colocación pasó validaciones de límites de repisas/mesadas; falta confirmación visual y de Play.
- El 2026-10-26 se anidaron en `ModularPrototype` 37 libros sueltos y 7 piezas de `low_poly_books` bajo sus estanterías: 40 en `Biblioteca_CentralDobleCara` y 4 en `Biblioteca_SurDerecha`. `Assets/Editor/LibraryBookHierarchyTool.cs` comprobó matrices mundiales y límites visuales tras el cambio; la auditoría final no encontró libros fuera de las cinco bibliotecas. Los adornos y el asset fuente no se modificaron. Escena guardada; Play no comprobado.
- El ZIP recibido de enciclopedias no incluía sus imágenes de textura: las tapas/páginas usan materiales de color provisionales. No afirmar que se restauró la textura original sin obtener los mapas faltantes.
- El FBX del celular en `Assets/Props/OldNokia` llegó con `back`, `screen` y `side` en URP Transparent y alfa 0.2. Se pasaron a Opaque con alfa 1; si un modelo importado se ve traslúcido, revisar primero los materiales. La escena conserva `StoryPhone` en el objeto padre y el renderer del greybox desactivado.
- El 2026-10-21 se integró `Door#5` del pack en `Assets/Props/PSXDoors` como visual de las 27 hojas de `ModularPrototype`; las bisagras y BoxColliders ProBuilder siguen siendo los funcionales. El menú `Arte > Integrar puertas PSX` re-alinea la visual sin duplicarla. Al ajustar un FBX, aplicar su rotación importada antes de encajarlo al collider; ignorarla convirtió grosor en altura.
- El cierre manual del jugador comienza aun dentro del umbral y desplaza su cápsula un poco al interior; el cierre automático sigue esperando umbrales libres. Cada `DoorSocket` crea en Play un `PisoUmbral` con collider a ras del piso. La cámara del portal debe ocultar el ensamblaje de retorno, pero excluir `PisoUmbral` de esa ocultación; ocultarlo mostraba exterior y mostrar todo el ensamblaje rompía la vista. El viajero con CharacterController cruza al llegar su borde delantero al plano para evitar que la pantalla recorte el cuerpo. El usuario verificó en Play niño–pasillo y pasillo–padres: suelo continuo, vista correcta, cierre inmediato sin caída ni recorte. La batería amplia `Historia y puertas` sigue pendiente de repetir; antes informaba fallos de sofá/NavMesh y supuestos antiguos de cierre, no darla por aprobada.

## Criterio de actualización
- Añadir solo decisiones duraderas, cambios realmente aplicados, resultados comprobados y errores concretos que convenga evitar; fechar un estado temporal y retirarlo cuando quede obsoleto.
- Mantener entradas breves y específicas; no copiar chats, logs extensos ni hipótesis como si fueran hechos.
- Nunca guardar claves, tokens, credenciales, datos personales ni rutas privadas de la computadora. Usar rutas relativas al proyecto.
