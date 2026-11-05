# Audio interactivo: integración y evidencia

## Bloque A — Mixer y mezcla final

`Assets/Audio3D/GameAudioMixer.mixer` es el único mixer del juego. Jerarquía: Master → Música, SFX, Ambiente y Voz. El snapshot inicial deja Master en −6 dB, Música en −8 dB, SFX en −3 dB, Ambiente en −6 dB y Voz en 0 dB. Ambiente tiene un Lowpass para suavizar las frecuencias altas de los emisores del entorno. Son valores de partida: hay que comprobar los picos y el balance escuchando la mezcla final en Play.

- Música: fuentes 2D creadas por `AdaptiveMusicDirector`; el tema del menú ya está conectado.
- SFX: pasos, aterrizaje, puertas, vibración del celular y sonidos puntuales alienígenas.
- Ambiente: noche, exterior alienígena, ventanas, TV, radio, reloj y vigilancia.
- Voz: una fuente 2D de `StoryVoiceDirector` para grabaciones humanas.

Las AudioSource directas de `ModularPrototype` y `FinalAlienLandscape` tienen Output asignado. Los emisores creados por scripts se conectan al crearse; `AudioMixerRouting` revisa también fuentes de prefabs instanciados. Los prefabs de `Assets/Generated` no se modificaron.

## Bloque B — Música adaptativa

El mixer contiene seis snapshots. Solo cambia el nivel del grupo Música; Master, SFX, Ambiente y Voz conservan sus valores. Los clips se reproducen en fuentes 2D ruteadas a Música. `AdaptiveMusicDirector` resuelve el tema del menú y de persecución desde `Resources`. Los campos de exploración y anomalía siguen vacíos: al iniciar la partida el tema de intro debe bajar con un fade out de 1,25 s y detenerse.

| Snapshot | Evento del juego | Música en mixer | Transición | Clip o capas |
| --- | --- | ---: | ---: | --- |
| Menu | Inicio o retorno al menú | −8 dB | 1,25 s | `Antes del Amanecer_para proyecto.wav` |
| Exploracion | Comenzar partida | −8 dB | 1,25 s desde el menú | Sin clip; fade out de la intro |
| Anomalia | Objetivo `FindPhone` o `Survive` | −12 dB | 4 s; 3 s al salir de persecución | Sin clip asignado todavía |
| Persecucion | Primer alien entra en `AlienChaseState` | −6 dB | 0,35 s de entrada; 3 s de retorno | `El Umbral.wav`, volumen 0,81 desde `ChaseMusicSettings` |
| Distancias | Restan 45 s de supervivencia | −9 dB | 1,25 s | Cinco capas sincronizadas: bajo, batería, piano, coro y guitarra |
| Amanecer | Objetivo `Escape` | −6 dB | 5 s | Capas de Distancias; piano y coro entran al amanecer |

El video del menú sigue en loop sin audio propio. `Distancias` desplaza al director de música anterior cuando quedan 45 s: bajo primero, batería a los 30 s, piano y coro al amanecer, guitarra en la salida. Cada capa entra en 0,35 s y todas salen en 0,4 s al cerrarse la puerta. Para B, demostrar los tres estados musicales que sí tienen audio: intro, persecución y Distancias; no presentar Exploracion o Anomalia como música audible. Los niveles del mixer son ajustes iniciales: aún falta escucharlos y medir picos en una partida completa.

`FinalLayeredMusicSettings` permite ajustar el volumen de cada instrumento. Las cinco capas de Distancias tienen 5.856.000 muestras (122 s a 48 kHz), por lo que comparten la misma longitud de loop. La alineación musical dentro de cada archivo debe comprobarse al escucharlas juntas en Play.

## Bloque C — SFX para la tabla del parcial

| Sonido puntual | Evento | 2D/3D | Grupo | Variación |
| --- | --- | --- | --- | --- |
| Aterrizaje | El personaje toca el suelo tras saltar | 2D | SFX | Tres clips, pitch y volumen; evita repetir el anterior |
| Paso al caminar | Distancia recorrida | 2D | SFX | Pitch aleatorio |
| Paso al correr | Distancia recorrida o inicio de carrera | 3D | SFX | Pitch aleatorio |
| Puerta que abre | `OpeningStarted` | 3D | SFX | Pitch aleatorio |
| Puerta que cierra | `ClosingStartedBy` | 3D | SFX | Pitch aleatorio |
| Celular que vibra | Entrada a la oficina durante la búsqueda | 3D | SFX | No |
| Pregunta del alien | Investigación de un ruido sospechoso | 3D | Voz | Pitch aleatorio |
| Respuesta «sí» | El alien preguntado causó ese ruido | 3D | Voz | Pitch aleatorio |
| Respuesta «no» | Ningún alien preguntado es responsable de ese ruido | 3D | Voz | Pitch aleatorio |
| Llamado alienígena | Ve al jugador o no recibe confirmación de un ruido | 3D | Voz | Pitch aleatorio |

`Alien.prefab` referencia `Pregunta por responsabilidad.wav`, `Respuesta Si.wav`, `Respuesta No.wav`, `Llamado.wav` y `SFX/llamado_aliens.wav` para la persecución. La fuente vocal del alien es 3D y sale por Voz. Se retiraron la vocalización periódica de patrulla y todos los clips procedurales de respaldo del alien. Su fuente de pasos sigue ruteada a SFX, pero queda muda mientras `footstepClip` no tenga una grabación asignada. La respuesta «no» no confirma que el ruido sea amistoso y solo se reproduce una vez por estímulo. Al entrar en `AlienChaseState`, `chaseClip` usa la grabación. El usuario informó que las cuatro grabaciones anteriores se oyen bien en juego. La auditoría `Auditar voces alienígenas` pasó en Play con `failures=0` y comprobó clips, eventos y ruteo; faltan una escucha de patrulla/persecución y mediciones de mezcla. Capturar en el Inspector `SFXVariacion` del jugador para la evidencia de variación. Estas vocalizaciones no se cuentan como emisores fijos del bloque D ni acreditan las voces humanas del bloque E.

Se eliminó también del prefab el disparo directo de `SFX_AlienGrito.wav` en `onAlertStarted`, junto con el componente `SFXVariacion` del alien que conservaba tres `SFX_AlienIdle`. `SFXVariacion` sigue disponible para el jugador y sus otros usos. Tras esta limpieza se repitió la auditoría en Play con `failures=0`, 0 advertencias y 0 errores.

## Bloque D — Ambientes y emisores 3D

Dos loops de fondo existentes: `Ambiente de Noche` en la casa y `Ambiente Alien` durante la secuencia final. El segundo baja durante el fade de salida. Cinco tipos de emisores fijos, separados de los SFX de la tabla: luz de ventana (0,8–5 m), televisor (0,7–9 m), radio de cocina (1,5–8 m), reloj de pie (1,2–9 m) y luz de vigilancia (1,2–12 m). Usan Spatial Blend 3D y rolloff lineal. Para el PDF: capturar dos AudioSource en Play y una vista Scene donde se aprecien sus posiciones.

## Bloque E — Voces del relato

Hay nueve WAV en `Assets/Resources/AudioVoiceLines`. `StoryVoiceDirector` responde al inicio de la partida, a los cambios de objetivo y al descubrimiento final; `DialogueAudioManager` reproduce las líneas por el grupo Voz. Los campos serializados `opening`, `parentsMissing`, `anomalyBegins`, `phoneFound`, `sunrise` y `finalDiscovery` siguen declarados, pero la reproducción actual se resuelve por identificador desde Resources. Falta escuchar y verificar cada línea en una partida completa, confirmar que las grabaciones cumplen el requisito de voces humanas del equipo y reunir evidencia de edición en DAW: forma de onda, cadena de procesos, parámetros y explicación del antes y después.

## Bloque F — Programación y comprobaciones pendientes

`AdaptiveMusicDirector` modifica dos parámetros en tiempo real: volumen de la fuente y frecuencia de corte del filtro. Responde a `GameSessionState`, `StoryProgression.ObjectiveChanged` y `AlienController.ChaseStateChanged`; cuenta los aliens activos, mantiene `El Umbral` mientras quede alguno persiguiendo y hace una salida de 3 s cuando termina el último. También solicita las transiciones de snapshot con tiempos explícitos. Esto permite fundamentar F con lógica adicional a la sola selección de snapshots de B. `SFXVariacion` sigue variando otros SFX, pero no hace falta atribuirlo al bloque propio. Para el PDF, seleccionar hasta 40 líneas clave de `AdaptiveMusicDirector`, anotar objetivo, condición, los dos parámetros y el resultado realmente oído.

Antes de entregar el PDF: comprobar persecución, capas finales, nueve voces y todas las rutas de audio en Play; medir que Master no llegue a 0 dB en el segmento elegido; ajustar el balance de voz/SFX frente a Música; tomar capturas reales del mixer, emisores, variación de SFX, capas y DAW. No atribuir procesos de DAW ni autoría humana sin la evidencia del equipo. La compilación por sí sola no acredita esas escuchas.
