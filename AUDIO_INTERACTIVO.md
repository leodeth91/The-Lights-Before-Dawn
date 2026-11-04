# Audio interactivo: integración y evidencia

## Mixer

`Assets/Audio3D/GameAudioMixer.mixer` es el único mixer del juego. Jerarquía: Master → Música, SFX, Ambiente y Voz. El snapshot inicial deja Master en −6 dB, Música en −8 dB, SFX en −3 dB, Ambiente en −6 dB y Voz en 0 dB. Ambiente tiene un Lowpass para suavizar las frecuencias altas de los emisores del entorno. Son valores de partida: hay que comprobar los picos y el balance escuchando la mezcla final en Play.

- Música: fuentes 2D creadas por `AdaptiveMusicDirector`; el tema del menú ya está conectado.
- SFX: pasos, aterrizaje, puertas, vibración del celular y sonidos puntuales alienígenas.
- Ambiente: noche, exterior alienígena, ventanas, TV, radio, reloj y vigilancia.
- Voz: una fuente 2D de `StoryVoiceDirector` para grabaciones humanas.

Las AudioSource directas de `ModularPrototype` y `FinalAlienLandscape` tienen Output asignado. Los emisores creados por scripts se conectan al crearse; `AudioMixerRouting` revisa también fuentes de prefabs instanciados. Los prefabs de `Assets/Generated` no se modificaron.

## Música adaptativa

El tema `Assets/Music/Antes del Amanecer_para proyecto.wav` está configurado como música 2D, en loop y dirigido al grupo Música. Entra al abrir el menú, continúa durante los créditos y sale al comenzar la partida. El clip se resuelve mediante `MenuMusicSettings`, por lo que no depende de guardar cambios pendientes de la escena.

| Estado | Evento | Fade | Clip |
| --- | --- | ---: | --- |
| Menú y créditos | Se carga o se vuelve al menú principal | 1,25 s de entrada | `Antes del Amanecer_para proyecto.wav` |
| Silencio jugable | Se selecciona Comenzar | 1,25 s de salida | Sin clip |
| Persecución | Al menos un alien entra en `AlienChaseState` | 0,35 s de entrada; 3 s de salida al terminar la última persecución | `El Umbral.wav` |
| Distancias por capas | 45 s: bajo; 30 s: batería; amanecer: piano y coro; salida: guitarra | Entrada de 0,35 s por capa; corte al mirar la puerta | Cinco WAV sincronizados por `PlayScheduled` |
| Final | El jugador cruza la salida e inicia la secuencia final | Pendiente de decidir | Composición pendiente |

El video del menú sigue reproduciéndose en loop sin audio propio. Exploración y anomalía permanecen sin música hasta los últimos 45 segundos de supervivencia; desde allí Distancias tiene prioridad sobre cualquier persecución. El menú, la persecución y las capas finales ofrecen tres estados musicales diferenciados para el bloque B.

`FinalLayeredMusicSettings` permite ajustar el volumen de cada instrumento. Las cinco capas de Distancias tienen 5.856.000 muestras (122 s a 48 kHz), por lo que comparten la misma longitud de loop. La alineación musical dentro de cada archivo debe comprobarse al escucharlas juntas en Play.

## SFX para la tabla del parcial

| Sonido puntual | Evento | 2D/3D | Grupo | Variación |
| --- | --- | --- | --- | --- |
| Aterrizaje | El personaje toca el suelo tras saltar | 2D | SFX | Tres clips, pitch y volumen; evita repetir el anterior |
| Paso al caminar | Distancia recorrida | 2D | SFX | Pitch aleatorio |
| Paso al correr | Distancia recorrida o inicio de carrera | 3D | SFX | Pitch aleatorio |
| Puerta que abre | `OpeningStarted` | 3D | SFX | Pitch aleatorio |
| Puerta que cierra | `ClosingStartedBy` | 3D | SFX | Pitch aleatorio |
| Celular que vibra | Entrada a la oficina durante la búsqueda | 3D | SFX | No |
| Pregunta del alien | Percepción del jugador | 3D | SFX | Pitch aleatorio |
| Llamada de persecución alienígena | Inicio de la persecución | 3D | SFX | Pitch aleatorio |

Confirmar en Play los dos últimos eventos y capturar en el Inspector el componente `SFXVariacion` del jugador para la evidencia de variación. Estos SFX 3D se cuentan solo en el bloque C.

## Ambientes y emisores 3D

Dos loops de fondo existentes: `Ambiente de Noche` en la casa y `Ambiente Alien` durante la secuencia final. El segundo baja durante el fade de salida. Cinco tipos de emisores fijos, separados de los SFX de la tabla: luz de ventana (0,8–5 m), televisor (0,7–9 m), radio de cocina (1,5–8 m), reloj de pie (1,2–9 m) y luz de vigilancia (1,2–12 m). Usan Spatial Blend 3D y rolloff lineal. Para el PDF: capturar dos AudioSource en Play y una vista Scene donde se aprecien sus posiciones.

## Voces pendientes

`SISTEMAS_JUEGO > StoryVoiceDirector` tiene seis espacios vacíos: `opening`, `parentsMissing`, `anomalyBegins`, `phoneFound`, `sunrise` y `finalDiscovery`. Se disparan respectivamente al comenzar la partida, encontrar vacía la habitación de los padres, iniciar la anomalía, recoger el celular, amanecer y cruzar la salida final. Se requieren grabaciones humanas del equipo (seis líneas o 60 s si son 4–6 integrantes); todavía no hay clips ni evidencia del DAW. Al incorporarlos, limpiar ruido y respiraciones, normalizar y aplicar/documentar al menos EQ, compresión o de-esser. Conservar capturas de la forma de onda y de los parámetros del proceso.

## Programación y comprobaciones pendientes

`AdaptiveMusicDirector` modifica volumen y frecuencia de corte durante las transiciones. Responde a `GameSessionState` para el menú y a `AlienController.ChaseStateChanged` para la persecución: cuenta los aliens activos, mantiene el loop mientras quede alguno persiguiendo y hace una salida de 3 s cuando termina el último. `SFXVariacion` elige clips sin repetir el anterior y varía pitch y volumen al aterrizar. Para la evidencia de programación, seleccionar hasta 40 líneas clave y anotar objetivo, condición, parámetro y resultado oído.

Antes de entregar el PDF: incorporar persecución y final, añadir las voces, comprobar todas las rutas en Play, medir que Master no llegue a 0 dB en el segmento elegido, ajustar balance de voz/SFX frente a Música y tomar capturas reales del mixer, emisores y DAW. La compilación por sí sola no acredita esas escuchas.
