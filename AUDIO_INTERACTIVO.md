# Audio interactivo: integración y evidencia

## Bloque A — Mixer y mezcla final

`Assets/Audio3D/GameAudioMixer.mixer` es el único mixer del juego. Jerarquía: Master → Música, SFX, Ambiente y Voz. El snapshot inicial deja Master en −6 dB, Música en −8 dB, SFX en −3 dB, Ambiente en −6 dB y Voz en 0 dB. Ambiente tiene un Lowpass para suavizar las frecuencias altas de los emisores del entorno. Son valores de partida: hay que comprobar los picos y el balance escuchando la mezcla final en Play.

- Música: fuentes 2D creadas por `AdaptiveMusicDirector`; el tema del menú ya está conectado.
- SFX: pasos, aterrizaje, puertas, vibración del celular y sonidos puntuales alienígenas.
- Ambiente: noche, exterior alienígena, ventanas, TV, radio, reloj y vigilancia.
- Voz: una fuente 2D de `StoryVoiceDirector` para grabaciones humanas.

Las AudioSource directas de `ModularPrototype` y `FinalAlienLandscape` tienen Output asignado. Los emisores creados por scripts se conectan al crearse; `AudioMixerRouting` revisa también fuentes de prefabs instanciados. Los prefabs de `Assets/Generated` no se modificaron.

## Bloque B — Música adaptativa

El tema `Assets/Music/Antes del Amanecer_para proyecto.wav` está configurado como música 2D, en loop y dirigido al grupo Música. Entra al abrir el menú, continúa durante los créditos y sale al comenzar la partida. El clip se resuelve mediante `MenuMusicSettings`, por lo que no depende de guardar cambios pendientes de la escena.

| Estado | Evento | Fade | Clip |
| --- | --- | ---: | --- |
| Menú y créditos | Se carga o se vuelve al menú principal | 1,25 s de entrada | `Antes del Amanecer_para proyecto.wav` |
| Silencio jugable | Se selecciona Comenzar | 1,25 s de salida | Sin clip |
| Persecución | Al menos un alien entra en `AlienChaseState` | 0,35 s de entrada; 3 s de salida al terminar la última persecución | `El Umbral.wav` (volumen configurado: 0,81) |
| Distancias por capas | 45 s: bajo; 30 s: batería; amanecer: piano y coro; salida: guitarra | Entrada de 0,35 s por capa; salida de 0,4 s al cerrarse la puerta | Cinco WAV sincronizados por `PlayScheduled` |
| Cierre de Distancias | Se cierra la puerta en la secuencia final | 0,4 s de salida junto al golpe | Se detienen las cinco capas |

El video del menú sigue reproduciéndose en loop sin audio propio. Exploración y anomalía permanecen sin música hasta los últimos 45 segundos de supervivencia; desde allí Distancias tiene prioridad sobre cualquier persecución. El menú, la persecución y las capas finales ofrecen tres estados musicales diferenciados para el bloque B.

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

`AdaptiveMusicDirector` modifica volumen y frecuencia de corte durante las transiciones. Responde a `GameSessionState` para el menú y a `AlienController.ChaseStateChanged` para la persecución: cuenta los aliens activos, mantiene el loop mientras quede alguno persiguiendo y hace una salida de 3 s cuando termina el último. `SFXVariacion` elige clips sin repetir el anterior y varía pitch y volumen al aterrizar. Para la evidencia de programación, seleccionar hasta 40 líneas clave y anotar objetivo, condición, parámetro y resultado oído.

Antes de entregar el PDF: comprobar persecución, capas finales, nueve voces y todas las rutas de audio en Play; medir que Master no llegue a 0 dB en el segmento elegido; ajustar el balance de voz/SFX frente a Música; tomar capturas reales del mixer, emisores, variación de SFX, capas y DAW. No atribuir procesos de DAW ni autoría humana sin la evidencia del equipo. La compilación por sí sola no acredita esas escuchas.
