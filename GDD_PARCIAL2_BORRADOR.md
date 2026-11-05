# The Lights Before Dawn — GDD del Segundo Parcial (borrador)

**Estado:** texto de trabajo para el vertical slice. Completar integrantes, material visual y resultados actuales antes de entregar. Basado en el proyecto y en `MEMORY.md`/`TESTING.md`; las notas históricas no prueban la build final.

## High Concept

Juego 3D de exploración y sigilo: un niño despierta de noche en una casa cuyas conexiones cambian. Busca a sus padres, descubre que la salida lo devuelve al interior, intenta pedir ayuda, sobrevive a los aliens y escapa al amanecer hacia un paisaje desconocido. La tensión surge de orientarse en la casa mientras las puertas y la percepción de los enemigos alteran cada recorrido.

**Género:** exploración, sigilo y horror. **Objetivo del jugador:** sobrevivir hasta el amanecer y cruzar la salida final. **Público/tono y referencias visuales:** [completar con referencias e imágenes autorizadas].

## Core gameplay y loop

1. Observar la habitación y el objetivo en el HUD; recorrerla en tercera persona.
2. Abrir puertas y cruzar conexiones; durante la anomalía, comprobar el destino visible antes de atravesar.
3. Escuchar señales, evitar la visión alienígena y usar escondites o desplazamiento sigiloso.
4. Cumplir el objetivo narrativo para avanzar de fase y, durante supervivencia, resistir hasta el amanecer.

El código de `StoryProgression` ordena **buscar padres → intentar salir → buscar teléfono → sobrevivir → escapar**. En `Survive`, `GameSessionManager` cuenta el tiempo restante; el amanecer habilita la salida final. La duración serializada y la sensación real del loop deben comprobarse en una partida actual.

## Diseño del mapa y Path Route

Escenario principal: `Assets/Scenes/ModularPrototype.unity`, con once módulos de habitación según las auditorías históricas. La casa contiene habitación infantil, pasillos, dormitorio de padres, oficina, recibidor, sala/comedor, cocina, biblioteca, baños y hall de escalera. `HouseFlowController` conecta puertas y portales, y altera destinos durante la anomalía. El exterior de cierre está en `FinalAlienLandscape.unity`.

**Ruta crítica propuesta por los objetivos del código:** cama infantil → dormitorio de padres → puerta de salida y retorno anómalo → oficina y celular → supervivencia en la casa → puerta de salida al amanecer → paisaje final. **[Insertar mapa o esquema real del nivel y capturas de la ruta; verificar la ruta en Play de principio a fin.]**

## Mecánicas y sistemas integrados

| Sistema | Implementación presente | Demostración pendiente de la versión actual |
| --- | --- | --- |
| Movimiento, cámara e interacción | `ChildPlayerController`, `OverShoulderCamera`, `PlayerInteractor`, `CharacterController`; raycast e interfaces de interacción | Control, giro, cámara, saltos, objetos y puertas en partida normal |
| Sigilo e IA | `AlienController` con estados, percepción, búsqueda, navegación y escondites; `AlienPopulation` intenta crear Alien 3 al iniciar `Survive` | Persecución libre entre puertas y plantas, aparición y movimiento de Alien 3, captura y escape |
| Puertas y anomalía | `DoorSocket`, `RoomConnection`, `HouseFlowController`, portales y viajeros | Visión y cruce sin cielo, duplicados ni caída; cierre y cambio de destino bajo persecución |
| Narrativa y UI | `StoryProgression`, menú, HUD, pausa y voces por evento | Progresión completa, Escape y botones de pausa, créditos definitivos |
| Cinemachine y animación | Despertar y secuencia final dirigidos por cámara; animaciones del niño, puertas y alien | Encuadres, transiciones y movimiento visual en partida completa |
| Audio | `GameAudioMixer` con Música/SFX/Ambiente/Voz, música de persecución y final por capas, ambientes 3D y voces | Escucha de eventos, mezcla, ruteo y picos en Play; ver `AUDIO_INTERACTIVO.md` |
| Iluminación y terreno | Noche, anomalía, amanecer y escena exterior con terreno/vegetación | Legibilidad y transición visual en Game/Play |
| Partículas | `FinalAlienLandscape.unity` contiene un `Particle System` activo, en loop y con `playOnAwake` | Verificar que sea visible al llegar al exterior y documentar su función; la presencia serializada no demuestra integración visual |

## Desafío final integrador

Tras encontrar el celular, el jugador debe sobrevivir a la presencia alienígena hasta el amanecer. Entonces se detiene la amenaza, la puerta de salida conecta con `FinalAlienLandscape` y comienza una secuencia de cámara, caminata y cierre narrativo que retorna al menú. `FinalSequenceRuntimeAudit` volvió a registrar `fallos=0` el 05/11/2026, incluida la aparición de Alien 3 sobre NavMesh, el cruce y el retorno al menú. El auditor acelera el contador y no representa una partida normal. Falta validar recorrido, encuadre y cierre de puerta en juego continuo, además del audio.

## Evolución respecto del Primer Parcial

El registro del proyecto documenta la expansión del greybox modular con mobiliario, materiales, ventanas, iluminación nocturna, terreno exterior, IA, audio adaptativo, UI y secuencias. **[Añadir comparativa real de capturas del Parcial 1 y la versión actual, con fecha y fuente de cada imagen.]** Mantener separados sistemas observados en Play y sistemas solo inspeccionados.

## Estado actual y próximos ajustes

**Verificado en esta revisión:** arranque breve desde el menú, HUD, dos aliens con aspecto anterior y auditorías focalizadas de persecución por portal y final (`fallos=0`); Alien 3 apareció y los tres comenzaron sobre NavMesh en el auditor. Detalles y límites en `TESTING.md`. **No verificado:** una partida completa actual, persecución libre con tres aliens, pausa con teclado físico, escucha/medición de mezcla y build ejecutable. La visual funcional de `Alien.prefab` conserva el aspecto anterior sin la textura nueva.

**Prioridad antes de entregar:** (1) partida completa y regresión de puertas/IA; (2) pausa y secuencia final; (3) escucha/medición de audio; (4) captura real de evidencia, build y accesibilidad de enlaces. Registrar cualquier fallo conocido en la página de entrega.

## Equipo, créditos y referencias

**Integrantes y roles:** [completar con el equipo]. **Recursos visuales/sonoros externos:** [listar procedencia y licencias verificadas]. **Repositorio, build y video:** [insertar enlaces accesibles después de publicarlos].
