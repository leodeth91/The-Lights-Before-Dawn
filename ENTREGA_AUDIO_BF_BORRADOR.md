# Audio Interactivo — borrador individual de los bloques B y F

**Entrega:** 05/11/2026, 23:59. **Estado:** borrador para trasladar a `Plantilla_Parcial2_AudioInteractivo.docx`; faltan escucha, capturas, datos del equipo y video. No usar como PDF final sin completarlos.

## Parte 1 — ficha común del equipo (1–2 páginas)

- Equipo e integrantes: **[completar con nombres y bloques A–F]**.
- Proyecto base: **[confirmar opción 1, 2 o 3]**. Segmento jugable de 1–2 minutos: **[describir dos situaciones y acciones que efectivamente aparezcan en el video]**.
- Video público o no listado (720p o más, máximo 6 min): **[enlace y duración]**. Bloques A–F en orden, con titular y minuto: **[completar tras editar el video]**.
- Visión sonora, 3–5 líneas: **[redactar con el equipo; indicar tono, referencias y función jugable del sonido]**.
- Captura del Audio Mixer completo: **[insertar captura real con niveles en reposo]**.

## Parte 2 — mi rol: B, música adaptativa; F, programación de audio

### Bloque B — estados y eventos implementados

| Estado | Disparador | Transición configurada | Evidencia que falta |
| --- | --- | --- | --- |
| Menú y créditos | Estado `Menu`; termina al seleccionar Comenzar | Entrada y salida de 1,25 s | Captura de fuentes/capa y escucha de entrada, loop y salida |
| Persecución | Primer alien entra en `AlienChaseState`; sale al terminar la última persecución | Entrada 0,35 s; salida 3 s; volumen de `ChaseMusicSettings` = 0,81 | Escucha del cambio, loop, balance y persecuciones simultáneas |
| Distancias final | 45 s antes del amanecer: bajo; 30 s: batería; `Escape`: piano y coro; apertura final: guitarra | Capas sincronizadas con `PlayScheduled`; entrada de 0,35 s; salida de 0,4 s tras el cierre final | Captura de las cinco `AudioSource`, escucha de sincronía y cierre |

Archivos para inspección: `Assets/Scripts/Audio/AdaptiveMusicDirector.cs`, `FinalLayeredMusicDirector.cs`, `Assets/Resources/Audio/ChaseMusicSettings.asset` y `FinalLayeredMusicSettings.asset`. Los cinco WAV están asignados al segundo asset. **La presencia del código y las referencias no prueba el resultado audible.**

### Bloque F — programación de audio

Para el requisito de dos scripts (o uno que controle dos parámetros), se pueden mostrar estos dos componentes, sujetos a demostración en Play:

1. `AdaptiveMusicDirector`: cuenta los aliens en persecución y solo cambia el estado musical cuando el conteo cruza cero. Durante la transición interpola **volumen** y **frecuencia de corte** del filtro. Fragmentos recomendados para el PDF: `Update` (líneas 94–108) y `OnAlienChaseChanged` (145–157); juntos ocupan menos de 40 líneas de código. Resultado audible: **[describir después de escucharlo, con minuto del video]**.
2. `FinalLayeredMusicDirector`: condiciona la entrada de capas al tiempo restante y al objetivo; programa los cinco clips sobre el mismo reloj DSP y ajusta su **volumen**. Fragmento recomendado: `Update` y `StartLayers` (líneas 49–73), menos de 40 líneas. Resultado audible: **[describir después de escucharlo, con minuto del video]**.

Para cada script, completar en la plantilla: **objetivo**, **condición**, **parámetro modificado**, **resultado audible**. Adjuntar captura de código legible y captura de las fuentes en Play; consignar los minutos exactos del video.

### Decisiones a fundamentar en primera persona

Completar cada fila con una decisión propia y verificable; la consigna pide al menos tres y una por bloque.

| Bloque | Decisión | Alternativa descartada | Motivo | Minuto donde se escucha |
| --- | --- | --- | --- | --- |
| B | **[completar]** | **[completar]** | **[completar]** | **[completar]** |
| B o F | **[completar]** | **[completar]** | **[completar]** | **[completar]** |
| F | **[completar]** | **[completar]** | **[completar]** | **[completar]** |

Trabajo con el equipo (5–10 líneas): **[qué recibí de A/C/D/E, qué entregué, cómo se integró]**.

Reflexión (5–10 líneas): **[dificultad concreta, resolución comprobada y mejora futura]**.

Recursos externos e IA: **[declarar origen real de cada audio, librería y herramienta; no inferir autoría ni procesos de DAW]**.

## Evidencias y validación que faltan hoy

- Grabar un tramo jugado **de corrido de 45–90 s**, sin audio añadido en edición, que permita oír dos situaciones, música, voces, SFX y ambiente con la mezcla final.
- Escuchar en Play los tres estados B, incluido el final por capas; comprobar sincronía, fades, prioridad frente a persecución y balance con Voz/SFX. Registrar hora, commit, escena, pasos y resultado observado.
- En el Mixer, medir que Master quede por debajo de 0 dB durante ese tramo. Guardar captura real de niveles en reposo y evidencia de medición. No declarar mezcla aprobada hasta escuchar y medir.
- Capturar los fragmentos de F y el Inspector de las fuentes musicales con valores, más el minuto del video donde se oye cada efecto de la lógica.
- Confirmar con el equipo los titulares y evidencias de A, C, D y E, especialmente origen humano y edición DAW de las voces.
