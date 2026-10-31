# Informe de testing — The Lights Before Dawn

**Versión del informe:** 30/10/2026

**Proyecto:** prototipo modular de exploración y sigilo 3D

**Escena principal:** `Assets/Scenes/ModularPrototype.unity`
**Entorno:** Unity 6.3 LTS (`6000.3.5f2`), URP 17.3.0, AI Navigation 2.0.9, ProBuilder 6.0.9, Input System 1.17.0 y Unity Test Framework 1.6.0.

## Objetivo y alcance

Este documento reúne las revisiones, preparaciones y pruebas registradas durante el desarrollo, como base para el PDF de la segunda instancia de evaluación parcial de **Motores de Desarrollo**. Las fechas corresponden a la ejecución o al registro de cada actividad, no necesariamente a la fecha del último commit. El informe no reemplaza una pasada completa de juego ni certifica que cada resultado histórico siga vigente después de cambios posteriores.

**Criterio de lectura:**

- **Validado en Play:** se ejecutó una prueba o una interacción dentro de Unity y quedó registrado el resultado.
- **Inspección estática o visual:** se comprobaron archivos, referencias, colocación, geometría o una vista de Scene, sin probar toda la mecánica en una partida.
- **Pendiente:** falta una prueba representativa o existe un fallo conocido.

## Registro cronológico

### 19/10/2026 — Percepción a través de portales

- **Preparación:** se corrigió `AlienPerception` para transformar el rayo de visión al atravesar el marco, comprobar obstáculos a ambos lados y bloquear la visión cuando alguna puerta está cerrada. `PortalTrialGate.MapDirection` conserva la dirección del rayo.
- **Validación:** el usuario comprobó en Play detección y persecución en ambos sentidos, oclusión por puerta y muro y cruce cerca del marco. No se registraron errores de compilación en esa revisión.
- **Estado:** validado en Play para los casos mencionados. Archivo principal: `Assets/Scripts/Aliens/AlienPerception.cs`.

### 20/10/2026 — Integración de mobiliario y libros

- **Preparación:** se colocaron libros en cinco estanterías, laptops en la habitación infantil y la oficina, un microondas y una visual nueva de cocina. Se conservaron los colliders funcionales de la cocina y la heladera.
- **Validación:** se comprobaron límites y ubicación de los modelos sobre repisas y mesadas mediante la herramienta de integración.
- **Estado:** inspección de colocación; quedó pendiente confirmar visualmente el conjunto y recorrerlo en Play. Herramienta: `Assets/Editor/PolyHavenPropIntegration.cs`.

### 21/10/2026 — Visual de puertas

- **Preparación:** se integró `Door#5` como visual de 27 hojas, conservando bisagras y `BoxCollider` de ProBuilder. La herramienta de integración ajusta la rotación importada y alinea el modelo con la colisión.
- **Validación:** se revisó la alineación de la visual respecto de la puerta funcional.
- **Estado:** inspección de integración; el registro disponible no documenta una pasada completa de las 27 puertas en Play. Herramienta: `Assets/Editor/PSXDoorArtIntegration.cs`.

### 26/10/2026 — Jerarquía de libros de biblioteca

- **Preparación:** 37 libros sueltos y 7 piezas de `low_poly_books` quedaron como hijos de sus estanterías: 40 en `Biblioteca_CentralDobleCara` y 4 en `Biblioteca_SurDerecha`.
- **Validación:** `LibraryBookHierarchyTool` comparó matrices mundiales y límites visuales antes y después. La auditoría no encontró libros fuera de las cinco bibliotecas.
- **Estado:** validación estructural aprobada; no se documentó prueba en Play. Archivo: `Assets/Editor/LibraryBookHierarchyTool.cs`.

### 27/10/2026 — Arranque, escondites, baño y ventanas

1. **Arranque de aliens y vídeo del menú.** Se desactivó inicialmente el `NavMeshAgent` del prefab alien para configurar su tipo antes de registrarlo. El vídeo de menú se recodificó a H.264 con BT.709. El arranque normal en Play mostró **0 errores y 0 advertencias**. La batería amplia `Patrulla y navegación` seguía fallando en traslados y cruces, con **14 incidencias** usando el prefab corregido y **16** con el original; esas incidencias no se atribuyeron a la corrección de arranque.
2. **Cámara en escondites.** La cámara pasa a primera persona al esconderse, oculta temporalmente la malla propia y vuelve a tercera persona al salir. Bajo las camas se sitúa a 0,14 m del punto oculto. La prueba `Cámara en escondites` ejecutó **7 casos en Play, `FAILURES=0`**, incluidas ambas camas; se revisaron capturas desde ellas. Archivo: `Assets/Tests/HideCameraRuntimeSmokeTest.cs`.
3. **Lavabo del baño superior.** `wash_basin_stand.glb` reemplazó la visual de `VanitoryDoble` conservando **7 colliders**. Se revisaron orientación, escala y ajuste; el arranque en Play no mostró errores ni avisos. Quedó pendiente comprobar el uso del baño por el jugador. Herramienta: `Assets/Editor/WashBasinStandPlacement.cs`.
4. **Aberturas y ventanas.** Se instalaron **12 ventanas** en 9 prefabs y se abrieron **11 muros**, conservando materiales y colisiones laterales; el cristal tiene colisión y las luces exteriores proyectan sombras. La prueba `Ventanas, muros y luz` verificó **12 cristales y 11 muros activos, `FAILURES=0`**. `Cámara en escondites` volvió a pasar **7 casos, `FAILURES=0`**. Archivo: `Assets/Tests/WindowOpeningRuntimeSmokeTest.cs`.

### 28/10/2026 — Materiales, cortinas e iluminación

1. **UV del baño superior.** Se corrigió la orientación de azulejos del muro con ventana sin cambiar material, geometría funcional ni colliders. `Ventanas, muros y luz` volvió a dar **`FAILURES=0`** con 12 ventanas y 11 muros.
2. **Cortinas.** Se prepararon **26 mallas finas con pliegues y UV** en 10 prefabs, con material URP traslúcido y sombra recortada. Se conservaron disparadores y puntos de escondite. Tras restaurar dos objetos ajenos retirados por una operación de guardado del prefab, las pruebas en Play dieron **`FAILURES=0`** para ventanas/muros y **`FAILURES=0`** para los 7 casos de cámara. La prueba `Visión, sonidos y captura` confirmó el caso del jugador escondido, pero terminó con **`FAILURES=1`** por la escala del torso al agacharse; ese fallo sigue abierto. Herramienta: `Assets/Editor/CurtainFabricTool.cs`.
3. **Ambiente nocturno.** Se ajustó una base ambiental azul grisácea, la reflexión ambiental y la intensidad de 12 focos exteriores de ventana. La vista Scene de la habitación infantil mostró más detalle en el muro previamente negro y menos contraste. **No se verificó una partida dentro de todas las habitaciones** después de este ajuste.
4. **Pared iluminada artificialmente.** Se identificó que `ENTORNO/Directional Light`, blanca y sin sombras, iluminaba paredes interiores a través de los muros. Se desactivó junto con `ENTORNO/LuzLuna`, manteniendo focos exteriores y base ambiental. La escena se recargó; **falta validación visual de este resultado durante una partida**.

### 29/10/2026 — Baúl, televisor, audio y navegación alien

1. **Baúl de la sala.** Se sustituyó su visual ProBuilder por el GLB animado, manteniendo colliders, puntos y eventos del escondite. La apertura y el cierre activan la animación. `Cámara en escondites` pasó **7 casos en Play, `FAILURES=0`**, incluida la articulación del baúl. Falta comparar visualmente el collider original con la tapa abierta. Archivo: `Assets/Scripts/Interaction/ChestVisualAnimation.cs`.
2. **Interferencia del televisor.** Se comprobó que `tv__small.glb` no trae clips de animación; `TVInterferenceScreen` genera ruido procedural en la pantalla durante Play. Unity cargó el componente y entró en Play sin errores nuevos. **No se verificó visualmente la pantalla en la sala**, que estaba inactiva en el menú inicial. Archivo: `Assets/Scripts/Interaction/TVInterferenceScreen.cs`.
3. **Audio espacial.** `SpatialWorldAudio` vinculó clips para vibración del celular, sonido de ventanas, estática de TV, radio de cocina y reloj. Los ambientales se configuran como fuentes 3D en bucle; el celular se activa una vez en la misión `FindPhone`, y el reloj aumenta su volumen al empezar la anomalía. Unity reconoció referencias y componente y entró al menú en Play con **0 errores y 0 avisos**. **Falta escuchar y medir alcance/volumen de cada fuente en una partida completa**. Archivo: `Assets/Scripts/Audio/SpatialWorldAudio.cs`.
4. **NavMesh de habitaciones.** Una auditoría inicial detectó dos puntos de búsqueda problemáticos, uno sobre un mueble del recibidor y otro aislado en el hall de la escalera. Se ajustó la selección de suelo alcanzable, sin mover muebles ni modificar prefabs. La auditoría final en Play recorrió las **11 habitaciones**, evaluó **102 rutas candidatas** y terminó con **0 fallos**. Archivo: `Assets/Tests/AlienNavigationAudit.cs`.
5. **Persecución por portal.** Se conservó temporalmente el destino de una puerta cerrada si un alien había registrado la persecución. La prueba específica en Play confirmó memoria de la habitación destino, retención del enlace tras cerrar, cruce físico del alien y agente sobre NavMesh, con **0 fallos**. Archivo: `Assets/Tests/AlienPortalPursuitAudit.cs`.
6. **Preparación de entrega por Git.** Se resolvió un bloqueo de push causado por un HDR de 114 MB que seguía en el historial y un GLB vigente de 101 MB que requería Git LFS. Se conservaron los contenidos y GUID; esto comprueba integridad de entrega, no jugabilidad.

### 30/10/2026 — Consolidación del informe

- Se contrastaron las notas de `MEMORY.md`, los scripts de prueba y el historial de Git para redactar este informe.
- **No se ejecutó una nueva batería de Play para crear este archivo.** Los resultados anteriores se consignan con la fecha en que quedaron registrados.

### Antecedente con fecha exacta no documentada — Cruce físico de portales

- El registro del proyecto indica que el usuario verificó en Play los cruces niño–pasillo y pasillo–padres: suelo continuo, vista correcta, cierre inmediato, sin caída ni recorte del cuerpo.
- Se conserva como evidencia histórica sin asignarle un día que no quedó documentado. La batería amplia `Historia y puertas` aún debe repetirse en la versión actual.

## Pruebas disponibles en el proyecto

Desde **The Lights Before Dawn → Pruebas** se pueden ejecutar las siguientes comprobaciones. La presencia de un script no significa que la versión actual esté aprobada.

| Prueba | Archivo | Último resultado documentado |
| --- | --- | --- |
| Cámara en escondites | `Assets/Tests/HideCameraRuntimeSmokeTest.cs` | 29/10: 7 casos, `FAILURES=0` |
| Ventanas, muros y luz | `Assets/Tests/WindowOpeningRuntimeSmokeTest.cs` | 28/10: 12 ventanas, 11 muros, `FAILURES=0` antes de los últimos ajustes de ambiente |
| Visión, sonidos y captura | `Assets/Tests/AlienDetectionRuntimeSmokeTest.cs` | 28/10: `FAILURES=1`, escala del torso al agacharse |
| Patrulla y navegación | `Assets/Tests/AlienRuntimeSmokeTest.cs` | 27/10: 14 incidencias con el prefab corregido; requiere nueva ejecución |
| Historia y puertas | `Assets/Tests/StoryRuntimeSmokeTest.cs` | Pendiente de repetir; el registro previo menciona fallos del sofá/NavMesh y supuestos antiguos de cierre |
| Auditar navegación de habitaciones | `Assets/Tests/AlienNavigationAudit.cs` | 29/10: 11 habitaciones, 102 rutas candidatas, 0 fallos |
| Auditar persecución por portal | `Assets/Tests/AlienPortalPursuitAudit.cs` | 29/10: memoria, cierre, cruce y NavMesh, 0 fallos |

## Incidencias abiertas y pruebas necesarias antes del PDF

1. **Repetir la batería de historia y puertas** tras los cambios de anomalía, materiales de guía y tiempo de cierre. Revisar sus expectativas antes de interpretar un fallo como regresión.
2. **Repetir patrulla y navegación** en la escena actual y observar persecuciones libres entre varias habitaciones y plantas. Las dos auditorías focalizadas del 29/10 no sustituyen esa pasada prolongada.
3. **Corregir o explicar el fallo de escala del torso al agacharse** detectado por `Visión, sonidos y captura`.
4. **Hacer una pasada visual de iluminación nocturna** en todas las habitaciones durante Play: legibilidad de texturas, ausencia de paredes iluminadas sin causa y sombras que entren por ventanas.
5. **Escuchar el audio 3D en contexto** y registrar distancias, balance con voces alien, activación única del celular y cambio de volumen del reloj.
6. **Comprobar visualmente** la estática de la TV, el ajuste de collider/tapa del baúl, la accesibilidad al lavabo y los assets integrados en cocina, oficina y biblioteca.
7. **Revisar los tests históricos de ventanas** antes de repetirlos: su script comprueba ambiente totalmente apagado, mientras que después se añadió una base ambiental para legibilidad. Registrar por separado cualquier cambio deliberado de criterio.
8. **Registrar evidencia para la entrega:** versión/commit probado, capturas o vídeo de cada caso, resultado de Console y fecha de ejecución. Este informe conserva resultados resumidos; no adjunta logs o capturas originales.

## Casos de regresión recomendados

- Al cruzar un portal, comprobar que no se vea el cielo, no aparezcan hojas duplicadas y nadie caiga por el umbral.
- Abrir y cerrar una puerta con el jugador y un alien cerca; confirmar que se respeten las reservas de paso y que una persecución conserve temporalmente el destino. Fuera de persecución, comprobar el cambio normal de destino tras el margen configurado.
- Entrar y salir de cada escondite; confirmar cámara, restitución de la malla del niño y que un alien solo detecte al jugador escondido al revisar directamente el lugar.
- Recorrer escaleras, habitaciones amuebladas y portales con aliens activos, observando tropiezos, detenciones y recuperación de ruta.

## Criterio para la versión PDF

Antes de exportar, actualizar los resultados pendientes con pruebas nuevas y adjuntar evidencia fechada. En el PDF conviene separar **objetivo**, **procedimiento**, **resultado esperado**, **resultado observado**, **estado** y **captura** para cada caso seleccionado. No transformar un resultado de compilación, una inspección en Scene o una prueba focalizada en una afirmación de jugabilidad integral.
