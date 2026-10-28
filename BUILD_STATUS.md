# BUILD_STATUS

Qué compila, qué se probó, dónde y cuándo. Lo no verificado va marcado `UNVERIFIED`.

## 2026-10-28 — Rendimiento: de dónde salen los batches (desglose) y prueba de static batching en los abetos (descartada)
Editor, play mode vía Unity MCP, `Mobile_RPAsset`.

- Nueva `PerfProbe.Breakdown()`: en el punto donde esté el papá, apaga un grupo del nivel por vez y anota cuánto bajan batches, proyectores y triángulos.
- Desglose en el punto más caro (campamento, 47.6, −0.5; 760 batches, 388 proyectores, 1020k triángulos). Ruido de ±20 entre lecturas:

| Grupo | −batches | −proyectores | −triángulos |
|---|---|---|---|
| Trees (520 abetos) | **221** | **143** | 60k |
| Debris | 64 | 38 | 77k |
| Grass | 60 | 16 | 251k |
| Bushes | 53 | 16 | 95k |
| GroundDetail | 53 | 18 | 147k |
| TerraceWalls | 43 | 66 | 212k |
| Landmarks | 43 | 68 | 17k |
| RoomLandmarks | 42 | 6 | ≈0 |
| Searchlights | 40 | 26 | 106k |
| Atmosphere | 39 | 10 | 96k |
| DryBranches | 34 | 24 | 102k |

- Corrección de una nota anterior: los abetos **no** usan material con viento (son URP/Lit: corteza + ramitas con recorte alfa, 2 draws por árbol). No tienen static batching porque el builder solo los marca `OccludeeStatic`.
- Prueba (sin guardar): abetos con `BatchingStatic` (el desvanecido por dithering es compatible: cambia materiales y no usa el origen del objeto). Resultado en los 6 puntos: batches **−22 a −28%** (605→440 … 746→584), pero draws **+7 a +41%** (785→931 … 740→1046) y proyectores de sombra **+14 a +85%** (301→426 … 260→480). Sin un iPhone para ver cuál pesa más en Metal, no se adopta; la escena quedó como estaba.
- `UNVERIFIED`: por qué el static batching sube los proyectores (sospecha: el LOD de impostor deja de recortar la sombra dentro del lote). Siguiente paso: medirlo con el Profiler en un iPhone, o probar menos árboles a la vista en vez de combinarlos.

## 2026-10-28 — Rendimiento: cantos sueltos del bosque sin sombra
Editor, play mode vía Unity MCP, `PerfProbe` con `Mobile_RPAsset`.

- Los 420 cantos sueltos (`Rocks`) están repartidos por todo el terreno; 298 quedan a más de 4 m del suelo caminable del diseño (bosque entre salas). Esos ya no proyectan sombra (`HollowBuilder.FarRockShadows`, regla `FarRockNoShadow = 4 m`, aplicada en el builder y en la escena guardada). Rocas de cobertura, acantilados y orillas del arroyo no cambian: dan forma al terreno y a los escondites.
- Herramienta: `HollowBuilder.WalkDistance(p)` expone la distancia al suelo caminable del diseño para QA.

| Punto | Proyectores (tras helechos) → ahora | Triángulos → ahora |
|---|---|---|
| (−55, 0) | 327 → 301 | 773k → 739k |
| (−24, 0) | 335 → 317 | 861k → 836k |
| (−1.5, 0.2) | 279 → 260 | 831k → 906k |
| (24, 0) | 290 → 269 | 697k → 660k |
| (47.6, −0.5) | 415 → 375 | 1011k → 924k |
| (60, −3) | 353 → 330 | 855k → 729k |

  Proyectores −5 a −10% en todos los puntos; batches casi iguales (505–746): el grueso de los batches no son las sombras. Los triángulos varían también por Lurkers y haces en vivo.
- Aspecto: la sala 2 con y sin esas sombras se ve igual; histograma de P3 idéntico (51% / 4.6%).
- Regresión: P3 **6/6**, P4 **5/5**, P5 **5/5**.
- Pendiente: los batches (500–750) siguen altos para móvil; hace falta ver qué los produce (pasto, árboles con viento, partículas). `UNVERIFIED`: efecto en un iPhone.

## 2026-10-28 — Rendimiento: helechos sin sombra y medida repetible (`PerfProbe`)
Editor, play mode vía Unity MCP, con el asset de URP `Mobile_RPAsset` forzado (el nivel Mobile está desactivado para Windows y el editor no deja activarlo).

- Nueva herramienta `PerfProbe.Run()` (en play): lleva al papá a 6 puntos fijos del nivel y anota batches, draws, SetPass, triángulos y proyectores de sombra (media de 20 cuadros por punto); sale sola de play mode. Sirve para comparar antes/después; no reemplaza el Profiler en un iPhone.
- Censo: de 3457 renderers, 2463 proyectan sombra; los 539 helechos de borde (Poly Haven reducidos a ~1.3k triángulos, con recorte alfa) eran el grupo más grande detrás de rocas y acantilados.
- Cambio: los helechos (`Hollow_FernClump_0–3`) no proyectan sombra (`DecimatedPropPrefab(..., castShadows: false)` en el builder y en los 4 prefabs).

| Punto | Proyectores antes → después | Batches antes → después | Triángulos antes → después |
|---|---|---|---|
| (−55, 0) | 403 → 327 | 685 → 606 | 851k → 773k |
| (−24, 0) | 373 → 335 | 549 → 513 | 904k → 861k |
| (−1.5, 0.2) | 368 → 279 | 601 → 507 | 1021k → 831k |
| (24, 0) | 390 → 290 | 685 → 551 | 909k → 697k |
| (47.6, −0.5) | 484 → 415 | 827 → 751 | 1117k → 1011k |
| (60, −3) | 398 → 353 | 626 → 664 | 810k → 855k |

  El último punto sube porque los Lurker y los haces del OVNI no están en el mismo lugar en las dos corridas (el nivel corre en vivo); los otros cinco bajan entre 7 y 26% de proyectores.
- Aspecto: la toma de la sala 4 con y sin sombras de helecho se ve igual (las matas están al borde de las salas, casi siempre ya en sombra); el histograma de P3 da exactamente lo mismo (51% sombra profunda, 4.6% islas de luz).
- Regresión: P3 **6/6**, P4 **5/5**.
- Sigue alto para móvil: 500–750 batches y 280–415 proyectores; lo siguiente son las rocas y acantilados (810 renderers, todos con sombra). `UNVERIFIED`: el efecto en FPS y temperatura de un iPhone.

## 2026-10-28 — Regresión completa de POLISH_PASS (P0–P8 + SNEAK)
Editor, play mode vía Unity MCP. Nueva `PolishSuite.RunAll()` (menú *Project Ridge/QA/Run all polish tests*): cada fase en su propia sesión de play con el nivel recién cargado; resumen en la consola al final y `runInBackground` vuelve a 0.

| Fase | Antes de este bloque | Ahora |
|---|---|---|
| P0 | 8/8 | **8/8** |
| P1 | 11/12 | **12/12** |
| P2 | 5/5 | **5/5** |
| P3 | 5/6 | **6/6** |
| P4 | 5/5 | **5/5** |
| P5 | 5/5 | **5/5** |
| P6 | 5/5 | **5/5** |
| P7 | 6/7 | **7/7** |
| P8 | 9/9 | **9/9** |
| SNEAK | 2/2 | **2/2** |

Corrida final: **64/64** en una sola pasada, sin excepciones en la consola.

- **Arreglo del juego (P1)**: con un objeto recogible al lado, ACCIÓN recogía en vez de derribar al Lurker por la espalda (o de golpear al que te ataca). `PlayerActions` resuelve ahora primero el combate urgente (derribo posible o Lurker cazando a alcance de golpe) y después pistas/objetos.
- **Arreglo del juego (reaparecer)**: `GameFlow` cancela el giro en el sitio pendiente al reaparecer (`PlayerController.CancelStandingTurn`); el papá podía terminar en el checkpoint un giro empezado antes de morir.
- **P7 (la prueba, no el juego)**: el giro medido lo alteraba `AutoStance`, que agachado junto a una cobertura con una amenaza cerca vuelve la espalda a la cobertura. La prueba se hace lejos de amenazas (−51, 0), con reflectores y apariciones apagados, y espera al estado de locomoción.
- **P3 (la prueba, no el juego)**: la cámara se aleja con `ThreatBlend` cerca de los haces, lo que falseaba la escala del papá; la altura se mide con los haces apagados y el histograma con los haces encendidos 0.2 s después (con 1 s entraba la animación de mirar alrededor). La línea base de "mirar adelante" espera a que la cámara se asiente tras el salto desde la última toma (antes 2.40 m por un amortiguado a medias; ahora 1.52 m).
- Histograma de P3 justo en el límite: **51%** de sombra profunda (pide ≥ 50%), 4.6% de islas de luz. Un cambio de iluminación lo puede tumbar.
- `UNVERIFIED` (sin cambios): pruebas en dispositivo, audio a oído, un humano terminando el nivel, rendimiento en iPhone/iPad.

## 2026-10-28 — Aviso de sigilo (cómo descubrir que el stick suave agacha)
Editor, play mode vía Unity MCP. Prueba nueva `PolishTests.Run("SNEAK")` → **2/2**; regresión `PolishTests.Run("P8")` → **9/9**.

- `SneakHint` (en el papá): la primera vez en la partida que camina de pie ≥ 1.2 s con un Lurker a ≤ 10 m (sin que lo esté cazando), el mensaje breve del HUD dice **"Ease the stick to sneak"** (táctil y control) o **"Press C to crouch"** (teclado, que siempre trota). Motivo: el sigilo ya funcionaba (stick < 0.35 cerca de un Lurker → agachado, pisadas de 1.2 m) pero nada lo enseñaba, y caminar de pie cerca del Lurker de la sala 2 hace que te oiga (lo que mataba al bot en cada corrida).
- Probado: con el stick suave (0.25) a 5 m del Lurker el papá va agachado y **no** sale el aviso; caminando de pie (0.6) sale una vez (alfa 1.00, texto del control usado); una segunda caminata no lo repite.
- Solo cerca de Lurkers (no de reflectores: ahí el ruido no importa) y nunca durante una persecución.
- `UNVERIFIED`: que un jugador entienda la frase y la aplique; textos en inglés (como el resto del juego).

## 2026-10-28 — Build de Windows actualizado
`Builds/Windows/ProjectRidge.exe` con el HUD por tamaño físico, el limitador y el arreglo de la sala 4: **0 errores, 0 advertencias, 260 MB**, `runInBackground` 0. Arrancado 40 s: escena cargada, Lurkers en el NavMesh, sin excepciones; el build no modificó ningún archivo del proyecto.

## 2026-10-28 — Sala 4: el bucle de muertes era un tronco en la ruta del Lurker
Editor, play mode vía Unity MCP. Nivel completo con `LoopBot.Run()`: **FINISHED en 265 s** (2 muertes, 1 captura; ninguna en la sala 4). Sala 4 sola desde su checkpoint en t = 0 (`RunFrom(23, 44)`, la fase en que antes moría al instante): **cruzada en 37 s sin muertes**. `ValidateLayout`: OK.

- Causa (la introduje el 2026-10-27): al llevar las paradas del oeste de Lurker_B a x = 32.4, su línea de patrulla atravesó el tronco de cobertura de (32.6, −1.2); el Lurker lo rodeaba por el oeste (visto en x ≈ 31.7), quedaba más cerca del checkpoint de lo previsto y la predicción del bot (líneas rectas) fallaba según la fase.
- Arreglo: la patrulla vuelve a x = 31.4 (libre del tronco) y el checkpoint se aleja a (22.4, 0.5): 9.4 y 10.8 m de las paradas (alcance 9), fuera del barrido del reflector S2.

## 2026-10-28 — Mezcla medida en una partida real y limitador maestro
Editor, play mode vía Unity MCP. `MixRecorder` (en el AudioListener) graba la salida final del juego durante una corrida del bot y `MixReport.Analyze()` la mide: loudness integrado (BS.1770, el medidor de P8), pico de muestra, muestras saturadas y loudness de corto plazo (3 s).

| Grabación | Integrado | Pico | Saturadas | Rango 3 s (p10–p95) |
|---|---|---|---|---|
| Antes, nivel completo (264 s) | −20.1 LUFS | **+1.3 dBFS** | **83** (t ≈ 210–214 s: radio + captura + persecución) | 7.6 LU |
| Con limitador, nivel (284 s) | **−15.2 LUFS** | **−1.0 dBFS** | 0 | 7.5 LU |
| Con limitador, campamento + radio + final (55 s) | −16.8 LUFS | −1.0 dBFS | 0 | 5.5 LU |

- `MasterLimiter` en el AudioListener (lo pone `SoundSynth.SetupScene` en Build All): +4 dB de ganancia, techo −1 dBFS con ataque instantáneo (la ganancia se calcula con la muestra actual: nunca pasa del techo) y liberación de 150 ms. Objetivo de juego en móvil −16 LUFS ± 2: dentro, sin aplastar la dinámica (el rango de corto plazo casi no cambia).
- `UNVERIFIED`: cómo suena (bombeo del limitador en la radio, balance música/efectos); costo del limitador en el hilo de audio de un iPhone (un bucle por muestra, debería ser mínimo).
- Visto en la corrida del nivel con el limitador (no relacionado con el audio): el bot murió en la sala 2 siete segundos antes que en las corridas buenas y entró a la sala 4 en otra fase de la patrulla de Lurker_B; como al reaparecer el Lurker vuelve siempre al mismo punto, repitió la muerte 8 veces entre el checkpoint y el escondite (28.6, −2.6) y FALLÓ. La corrida anterior del mismo día terminó (264 s). La entrada de la sala 4 sigue siendo frágil para el bot según la fase: `UNVERIFIED` para un jugador.
- Corrección de la entrada anterior: en táctil el juego ya resuelve el cruce de la sala 2. Con un Lurker cerca y el stick inclinado < 0.35, el sigilo automático agacha al papá (pisadas de 1.2 m); el bot camina a 0.45 (de pie, 3.5 m). Lo pendiente es que el jugador descubra esa mecánica.

## 2026-10-27 — QA: el bot y las pruebas salen solos de play mode; la muerte fija de la sala 2 → 3
Editor, play mode vía Unity MCP. Nivel completo con `LoopBot.Run()`: **FINISHED en 264 s** (2 muertes, 1 captura), igual que antes.

- `LoopBot` y `PolishTests` salen de play mode al terminar (`StopPlayWhenDone`, por defecto sí): editar un `.cs` con play mode activo después de una corrida recompilaba a mitad de partida (pasó 3 veces). Probado: tras un FAILED y un FINISHED el editor quedó en modo edición sin intervención.
- **Efecto de captura** (pendiente de la entrada de los arreglos del build): visto en `P2_3_paralysis.png` tras recargar el proyecto: desaturación, aberración cromática y grano. Funciona cargado desde disco.
- **Muerte fija a la salida de la sala 2** (en todas las corridas, cerca de (4.4, 0.6)): no era la vista. El bot pasa caminando a 3.9 m por detrás del Lurker de la sala 2, que lo **oye** (pisadas caminando: 3.5 m) y lo caza; el sigilo automático no lo agacha ahí (`crouched False` en el registro). Un jugador puede evitarlo con el stick apenas inclinado (sigilo: pisadas de 1.2 m). Se probó enseñarle al bot el oído y un paso en sigilo con el escondite junto a la salida: el tronco de cobertura obliga a rodear por el oeste, hacia la vista del Lurker, y el bot no encontró ventana en 75 s; se volvió a la ruta que termina. Pendiente de diseño `UNVERIFIED`: si un jugador humano lee que ahí hay que ir en sigilo.

## 2026-10-27 — HUD en iPhone y iPad: tamaño físico, área segura y mínimos de toque
Editor (sin dispositivo). Nueva prueba `HudLayoutCheck.Run()` (menú Project Ridge/QA): simula el área segura y la densidad de cada equipo, renderiza el HUD a su resolución nativa y comprueba que los controles quedan dentro del área segura, no se tapan y miden ≥ 44 pt (mínimo de toque de Apple). Capturas con el área segura en rojo: `hud_iPhone16.png`, `hud_iPhone16ProMax.png`, `hud_iPadPro11.png`, `hud_iPadAir13.png`. Resultado: **OK en los 4** (ACCIÓN 110 pt, OBJETO 84, linterna/pausa/curar 44). Regresión `PolishTests.Run("P8")` → **9/9**.

- **Antes**: el canvas escalaba con la pantalla (1920×1080, mezcla 0.5) y en iPad todo crecía ~70% (ACCIÓN 191–225 pt, ~7–8 cm; pausa y curar salían del área segura en el iPad Air 13"); en el iPhone 16 la linterna y la pausa medían 40 pt; el aviso "+1" tapaba el borde del botón OBJETO.
- **Ahora**: `HudScaler` fija 2.49 unidades por punto en móvil (3 px/pt en iPhone, 2 en iPad, según `Screen.dpi`): mismo tamaño físico en todos; en PC sigue el escalado por pantalla. Linterna y pausa a 44 pt; el aviso "+1" 14 pt más a la izquierda. `SafeAreaFitter.Simulated` permite probar el área segura en el editor.
- Corrección a la entrada de rendimiento: al reaparecer, los Lurker ya tienen 6 s sin ver ni oír y los que están a < 20 m vuelven al punto más lejano de su ruta (`respawnGraceSeconds`); el riesgo del checkpoint del campamento es menor que lo anotado.
- `UNVERIFIED`: en dispositivo (valor real de `Screen.dpi`, márgenes reales del área segura, que el tamaño se sienta bien con los pulgares).

## 2026-10-27 — Arreglos que solo se veían en el build: efecto de captura, viñeta de escondido, brillos
Editor y build de Windows (0 errores, 0 advertencias, 260 MB; 40 s sin excepciones). `PolishTests.Run("P2")` → **5/5**.

- **Perfiles de volumen vacíos**: `CaptureVolume` (desaturación, aberración, grano y viñeta azul de la captura del OVNI) y `HiddenVignette` (viñeta al estar escondido) guardaban sus componentes solo en memoria (`profile.Add` sin `AddObjectToAsset`): en el archivo eran `fileID: 0`. Funcionaban en la sesión del editor que los creaba; al recargar el proyecto o en cualquier build, **no tenían efecto**. Lo delató un error de la analítica de URP en el build (`VolumeProfileOverridesAnalytic`, NullReference). Ahora se guardan como subassets (comprobado en el archivo: perfil + 4 y + 2 componentes).
- **Emisión quitada en el build**: con GI `None`, URP quita `_EMISSION` al validar los materiales durante el build: los hongos luminosos, el vidrio del farol, el papel de las pistas y **los ojos del Lurker** no brillaban en el build. Todos con GI `RealtimeEmissive`.
- Los builders escriben `_Color` como lo deja URP (ida y vuelta sRGB) y los parámetros de fundido sin partículas suaves: ni el build ni una partida modifican materiales.
- `runInBackground` vuelve a 1 en el disco después de cada play mode del editor (lo pone el arnés de pruebas): se restaura a 0 en edición antes de cada build.
- `UNVERIFIED`: que el efecto de captura y la viñeta se vean bien en el build (comprobado que los componentes existen; no se jugó la captura en el build).

## 2026-10-27 — Build de Windows
`Builds/Windows/ProjectRidge.exe`: **0 errores, 0 advertencias, 260 MB** (antes 358 MB: mallas de roca más livianas). `runInBackground` en 0 (el primer intento salió con 1 porque el editor lo había dejado así tras play mode: se rehízo). Arrancado 40 s: escena cargada, los 4 Lurker en el NavMesh, sin excepciones en el log; 817 MB de memoria del proceso en PC (perfil PC). Incluye la pantalla de créditos. `UNVERIFIED`: build de iOS (el módulo de iOS no está instalado en esta máquina; no hay Mac).

## 2026-10-27 — Rendimiento: perfil iPhone restaurado, triángulos a la mitad, checkpoint de la sala 4 fuera de la vista
Editor, play mode vía Unity MCP. Nivel completo con `LoopBot.Run()`: **FINISHED en 266 s** (2 muertes, 1 captura del reflector tras la radio). `PolishTests.Run("P3")` → **6/6** (papá 9.7–11.2%, sombra 54%, islas de luz 3.6%).

- **Perfil iPhone perdido** (encontrado al medir): el nivel de calidad `Mobile` desapareció el 2026-10-20 al reserializarse `QualitySettings` (commit 1ba7a37), y `PC` excluye iPhone y Android: un build de iOS no tenía ningún nivel válido y lo de "perfil iPhone" del 2026-10-19 había dejado de ser cierto. `QualityBuilder.EnsureLevels()` (en Build All) lo vuelve a crear con los valores originales: `Mobile_RPAsset`, iPhone/Android → Mobile, Windows → PC. Probado en el editor forzando `Mobile_RPAsset`: mismo look.
- **Triángulos por cuadro** (perfil Mobile, 5 puntos del nivel; antes / ahora): cabaña 1752k → **847k**; resto 838k–1135k (render 4.7–5.8 ms en la RTX 4070). Desglose medido apagando grupos: `GroundDetail` era el 43% (752k): las piedritas de 7–20 cm usaban la malla de roca de 3000 triángulos; ahora una de 120. Rocas de terraza y sueltas de 3000 → 1600 triángulos (sin diferencia visible a la distancia de cámara, `rocks3000.png` / `rocks1600.png`); el colisionador sigue saliendo de la versión de 3000.
- Pendientes de rendimiento: 552–829 batches (árboles con viento sin combinar: 221), 326–485 proyectores de sombra. `UNVERIFIED`: todo en dispositivo (FPS, térmica, memoria).
- **Regresión encontrada y arreglada**: con el colisionador sacado de la malla de 1600, Lurker_B veía el checkpoint de la sala 4 (24, 0) desde su parada a 7.9 m y al reaparecer lo volvía a cazar (9 muertes seguidas). El colisionador vuelve a salir de la malla de 3000 y, para que no dependa de unos centímetros, el checkpoint pasa a (23, 0.4) y las paradas del oeste de Lurker_B a x = 32.4 (distancias 9.7 y 10.8 m; alcance de vista 9). Mismo riesgo visto en el campamento (parada de Lurker_C a 7.2 m del checkpoint): no produjo bucles en las corridas, `UNVERIFIED`.
- **Charcos de farol en la vista de juego**: con bloom se leían como discos naranjas; color a 2/3 (0.8, 0.24, 0.035). Las capturas `Shots.Capture` muestran menos bloom que la vista de juego: los juicios de look se hicieron con `ScreenCapture` de la vista de juego.
- **P3**: la pose baja era el idle tenso (la aparición con guion de la sala 1 activa un Lurker a ≤ 14 m) o la mirada atrás; la prueba ahora apaga la aparición y espera a que pasen ambos. Cada toma anota clip, altura de la cabeza, caída de cadera y tensión.

## 2026-10-27 — Dirección de arte: orillas de roca, charcos de farol sin anillos, P3 6/6
Editor, play mode vía Unity MCP. `PolishTests.Run("P3")` → **6/6** (papá 9.8–11.1%, sombra 56%, islas de luz 4.5%; cada toma ≥ 3.3%). Capturas: `banks_before.png` / `banks_after.png` (arroyo), `pools_after.png` (pasillo sala 2 → 3), `P3_after_0..4.png`.

- **Orillas del arroyo** (diferencia nº 1 frente a `target_look.png`): la cara alta del talud era terreno liso y estirado. Ahora lleva peñascos al pie del talud (del agua hasta 25 cm bajo el piso: nunca asoman sobre la orilla ni tapan el paso; sin colisionador, el NavMesh no cambia: 10 999 vértices).
- **Charcos de farol**: tenían anillos escalonados. Causa (probada aislando): las partículas suaves sobre un quad a 4 cm del suelo (la diferencia de profundidad cuantizada). Sin ellas, liso; el color baja a la mitad porque antes el efecto lo atenuaba casi entero. Las brasas del choque conservan las partículas suaves (material propio `FX_EmberGlow`: el suelo es irregular y el quad cortaba recto). Descartado en el camino: compresión/8 bits de la textura y el dithering de cámara (no cambiaban nada).
- **Un farol más por pasillo** (10 en total): a la entrada de cada sala, junto al checkpoint, del lado opuesto al de salida. Marca el lugar seguro y da las islas cálidas de la referencia.
- **P3 estable**: la escala del papá fallaba al azar. Se vio una pose baja (cabeza a 1.27 m en vez de 1.52) en `Idle_Loop`; la prueba ahora espera también a que termine la mirada atrás de P7 y anota clip, altura de la cabeza y caída de cadera del IK en cada toma. En 3 corridas posteriores no se repitió: causa de aquella pose `UNVERIFIED`.
- Pendiente visto en la toma del campamento: el círculo del reflector rojo S4 tiene el borde iluminado y vuelve a leerse como objeto sólido.

## 2026-10-27 — Assets estables en git (sin cambios fantasma tras jugar)
Editor vía Unity MCP. Build All + una partida de 20 s: **0 assets modificados por la partida** (antes ~20 `.mat` por sesión).

- `Tinted` (HollowBuilder) y `Mat` (ItemsSetupBuilder) fijan también `_Color`; los materiales con emisión usan GI `RealtimeEmissive` (con `None`, URP quitaba `_EMISSION` de la radio, el domo de la cabina y la bengala del inventario al validar); las partículas fijan las distancias de las que URP recalcula `_SoftParticleFadeParams`.
- `WaterFlow` anima una copia del material del arroyo (antes movía el asset compartido y quedaba con el desplazamiento de la última partida).
- Conocido: las mallas `GroundDetail_*` cambian sus bytes en cada Build All aunque la semilla es fija (causa no encontrada; sin diferencia visible).

## 2026-10-27 — El nivel completo se termina con el bot (P9 cerrado)
Editor, play mode vía Unity MCP. `LoopBot.Run()` (solo stick + ACCIÓN + OBJETO) desde el título: **FINISHED en 242 s, 2 muertes, 0 capturas, 3 ACCIÓN, 1 OBJETO** — dos corridas seguidas con el mismo resultado. Regresión `PolishTests.Run("P8")` → **9/9**.

- **El atasco de P9 en la sala 5 estaba mal diagnosticado**: no faltaban ventanas (el tramo checkpoint → escondite es seguro buena parte del ciclo), el bot cortaba en línea recta la esquina de la entrada al campamento contra un borde invisible (`Bounds/Edge`) y se quedaba empujando a la vista hasta que Lurker_C lo veía. Ahora el bot sigue las esquinas del NavMesh entre sus puntos de ruta (9 esquinas en todo el nivel).
- **Clímax de la radio** (cambio de juego): el primer chillido llega 3 s después de encender la radio (`RadioClimax.firstSquealDelay`). Antes el chillido era inmediato y Lurker_C, a ~9 m, giraba hacia el ruido y veía al papá junto a la radio: muerte segura también para un jugador. Con la demora: los Lurker investigan y buscan alrededor de la camioneta, el papá aguanta en el pasto del suroeste (60.3, −5.7) y la transmisión termina en el final.
- Herramientas de QA nuevas: `LoopBot.RunFrom(x)` empieza en el último checkpoint con x ≤ x (estudiar una sala sin recorrer todo); el informe del bot registra cada cambio de estado de un Lurker (con distancia y si el papá estaba agachado) y un estado cada 5 s.
- Las 2 muertes (iguales en ambas corridas): a la salida de la sala 2 el Lurker lo caza en el cruce hacia la sala 3, y en la sala 5 Lurker_C investiga un ruido junto al escondite (51.7, −5.5). Ambas reaparecen en su checkpoint y siguen.
- P8: la prueba de capas de música fallaba al azar (el reflector de la sala 1 pasaba a ≤ 10 m del punto "en calma" según la fase y daba tensión con razón). El punto en calma pasa a la salida de la cabaña (16 m del reflector).
- `UNVERIFIED`: que un jugador humano en táctil lo termine (el bot no tiene que leer la pantalla ni apuntar con el pulgar); rendimiento en dispositivo.

## 2026-10-27 — Créditos en el juego (atribución CC BY de 100STYLE)
Editor, play mode vía Unity MCP. Capturas: `credits_pause.png`, `credits_end.png`, `credits_end_buttons.png`, `ending_text.png`.

- **Pantalla de créditos** (probado): botón CREDITS en el menú de pausa y en el final de la demo (PLAY AGAIN · CREDITS · QUIT). Dos columnas con todas las licencias de ASSETS.md; la atribución CC BY 4.0 de 100STYLE lleva autores, licencia con link, fuente y "clips cut and retargeted" (los cambios). Texto en `Assets/_Project/Data/Credits.txt` ("||" separa las columnas), armada por `HudBuilder.BuildFlow`.
- Probado: abrir desde la pausa oculta el menú de pausa; Escape (botón de pausa) cierra los créditos y vuelve la pausa (ACCIÓN también los cierra por código: `UNVERIFIED`, no se probó); desde el final, CREDITS → BACK devuelve los botones. Consola sin errores ni advertencias propias.
- Fondo opaco: con alfa 0.96 la escena se veía casi entera detrás (la UI con alfa se mezcla mucho más transparente que lo pedido en este proyecto, a revisar en los otros paneles: pausa 0.72, pista 0.86).
- Final: "end of the demo" estaba a 20 unidades (bajo el mínimo de 14 pt de P8); ahora título a 56 y subtítulo al mínimo.
- El build de Windows de P9 (ya distribuido localmente) no tiene esta pantalla; el próximo build la incluye.


## 2026-10-23 — POLISH PASS P9: pase final (bot, haces, agua, build de Windows)
Editor (play mode vía Unity MCP) y build de Windows. Capturas: `P9_after.png`, `P9_after_beam.png`, `P9_after_creek.png`, `compare_P9.png`.

**Aceptación de P9 — resultado honesto:**
- **Nivel completo sin ayuda con el bot: NO PASA.** `LoopBot.Run()` (solo stick + ACCIÓN + OBJETO) llega como máximo a **x = 49 (sala 5, el campamento) con 2 muertes** y se atasca 75 s esperando una ventana hacia el escondite (52, −4.6): el reflector S4_Gate barre la salida y Lurker_C se detiene en la esquina (54.8, 0.2) mirando al punto de reaparición. Otras corridas mueren 9 veces en el escondite justo después del puente (sala 2). El resultado varía entre corridas según el ritmo de cuadros. `UNVERIFIED`: que el nivel se pueda terminar; las salas 1–4 sí se cruzaron en corridas reales.
- Lo que se arregló en el bot (QA, no el juego): cuenta el giro en el sitio y el arranque animado de P7 al calcular la ventana, mira el tramo completo hasta el próximo escondite (no espera a la vista en los puntos de paso), lanza la piedra de distracción lejos de la ruta (antes atraía al Lurker al escondite siguiente) y predice la patrulla exacta (`EnemyController.PredictPatrol`: puntos, velocidad, pausas y el giro en cada parada). Al atascarse informa el motivo.
- **Lista de la sección 4:**
  - [ ] 60 s sin animaciones que patinen o floten a 0.25×: `UNVERIFIED` (revisados a 0.25× arranque y frenada; no una partida de 60 s).
  - [x] Ningún objeto en la mano sin motivo: el garrote va enfundado al moverse (P0) y solo sale con amenaza; la linterna, con la luz encendida.
  - [ ] Anticipación, contacto, recuperación y sonido en toda acción: en parte. Los golpes y la captura tienen las tres fases; que todo "suene bien" es `UNVERIFIED` (nadie escuchó la mezcla).
  - [ ] Terminar el nivel en táctil con dos pulgares: `UNVERIFIED` (el bot no lo termina; sin prueba en iPhone).
  - [~] Mismo "mundo" que `target_look.png`: paleta y escala cercanas; terrazas y plano medio todavía lejos (ver diferencias).
  - [x] 0 errores y 0 advertencias propias en la consola: en las últimas corridas solo aparece la advertencia de cuenta de Unity AI (no es del proyecto). El build tenía 2 advertencias de shaders propios (`_FORWARD_PLUS` obsoleto en DitherFade y MossyRock): corregidas, build final con 0.
- **Haz del OVNI** (probado en captura): el "balde" rojo era la base ancha del cono cortada por el terreno + el círculo del suelo con un borde duro. El haz se apaga en los últimos 2.5 m antes de la superficie (contacto suave por profundidad de escena) y el círculo pierde el anillo de borde duro y baja de 1.6 a 0.95 de intensidad.
- **Agua del arroyo** (probado en captura): el blanco quemado aguas arriba del puente era el brillo especular de la luna (seguía con suavidad 0.15). Sin brillo especular directo y con el reflejo del entorno se lee como agua oscura.
- **Regresión P3** (probado): **5/6**. Histograma: sombra 54%, luces 3.0% (los charcos de los faroles crecen de 3.4 a 4.2 m: el brillo del agua contaba como "isla de luz"). Escala del papá: 9.7–11.1% en 4 de 5 puntos; en (24, 0) se queda agachado (sigilo automático) y mide 7.5%: causa no confirmada. La prueba ahora espera a que se levante antes de medir.
- Idle tenso limitado al 50% sobre el idle normal (a 100% el estilo "BentForward" lo doblaba).
- **Build de Windows** `Builds/Windows/ProjectRidge.exe` (358 MB, 0 errores, 0 advertencias): arrancado 35 s, sin excepciones en `Player.log`. `runInBackground` restaurado a 0 antes del build.
- **Comparación con la referencia** (`compare_P9.png`), 5 diferencias:
  1. Terrazas que se leen como losas oscuras de borde recto. *No atacada*: requiere rehacer la forma del nivel (navmesh, escondites, pruebas de P4).
  2. Plano medio vacío frente a la utilería narrativa de la referencia (campamento, muelle, agua con reflejos).
  3. Cuadro más oscuro y verde que el ámbar de la referencia. *Atacada en parte*: charcos de farol más grandes (más islas cálidas).
  4. Arroyo blanco quemado. *Atacada*: sin especular directo.
  5. Haz y círculo rojos que parecían un objeto sólido. *Atacada*: contacto suave y círculo sin borde duro.
- `UNVERIFIED` de todo el pase que sigue abierto: rendimiento en iPhone/iPad, hápticos en dispositivo, mezcla de audio escuchada, controles táctiles en dispositivo, safe areas de iPad.

## 2026-10-21 — POLISH PASS P8: música por capas, loudness, HUD mínimo y hápticos
Editor, play mode vía Unity MCP. Prueba: `PolishTests.Run("P8")` → **9/9 PASS**. Capturas: `P8_before.png` (copia de `P5_after_hunting.png`, el HUD anterior: no se tomó una captura propia antes de cambiar), `P8_after_explore.png`, `P8_after_lowhealth.png`, `P8_after_pickup.png`.

- **Música** (probado): tres capas con crossfade de 1.5 s según el estado — explorar (dron "Abandoned passages"), tensión (sospecha > 0.3, Lurker a ≤ 12 m o haz a ≤ 10 m; pulso "Pulse" + latido) y persecución ("Urgent"). Medido: calma → Explore 1.00, Lurker a 9 m → Tension 1.00 (Explore 0.00), cazado → Chase 1.00. Stingers: pista (piano), detectado y captura (cuerdas "abyss", la captura más grave), checkpoint ("Victory"), final ("Transmission"). Todo CC0 de OpenGameArt (FreePD cerró).
- **Loudness** (probado): medidor BS.1770-4 propio (`MusicBuilder.Integrated`, verificado: seno de 997 Hz a −20 dBFS = −20.00 LUFS; mismo resultado que un cálculo independiente en Blender/numpy). 89 clips medidos; bajar volumen es una ganancia en el banco; subirlo se hace con copias normalizadas en `Assets/_Project/Audio/Normalized/` (`Tools/Blender/normalize_audio.py`, pico ≤ −1 dBFS). Música en −20 (capas −20.0/−20.0/−19.9), ambiente −24. **43 efectos transitorios** (pasos, golpes) no llegan a −16 sin saturar: su pico ya está en −1 dBFS; quedan marcados `peakLimited`.
- **HUD** (probado): fuera contadores, texto del arma y el indicador SAFE/HIDDEN/ALERT/HUNTED. Corazón arriba a la izquierda solo con vida < 100% o al tocarlo; viñeta roja y cojera (cadera que cae al apoyar la pierna izquierda) con vida < 35%; aviso de recogida (ícono + "+1") 1.2 s junto a OBJETO; botón OBJETO con ícono; objetivo de una frase arriba al centro 4 s al cambiar y siempre en pausa (frases acortadas). Explorando con vida llena: **0 elementos** a la vista (máx. 4).
- **Tipografía** (probado): Barlow Condensed SemiBold (OFL) en todo el HUD, mínimo 35 unidades = 14 pt en un iPhone de 6.1". El overlay de depuración conserva la fuente de Unity (no es HUD de juego).
- **Hápticos**: `HapticEvents` pide impacto fuerte al recibir daño y al empezar a cazarte un Lurker; leve al conectar un golpe, al disparar la bengala, al recoger y en el checkpoint (probado en el editor: 2 impactos pedidos por daño + recogida). `UNVERIFIED`: que vibren en un iPhone.
- `UNVERIFIED`: cómo suena la mezcla (nadie la escuchó; solo hay medidas), los bucles de las copias OGG (posible clic en el punto de bucle del latido), el HUD en un iPhone real y en iPad (safe areas).
- Visto en las capturas y pendiente para P9: el agua del arroyo aguas arriba del puente se ve blanca quemada (reflejo de la luna), ya estaba en P7.

## 2026-10-21 — POLISH PASS P7: animación completa (arranques, frenadas, pivote, giro en el sitio, IK de pies)
Editor, play mode vía Unity MCP. Prueba: `PolishTests.Run("P7")` → **7/7 PASS**. Regresiones: `P0` **8/8** (D2 ahora mide el giro en movimiento, ver DECISIONS), `P1` **12/12**. Capturas: `P7_after_start_025x.png` y `P7_after_stop_025x.png` (tiras a 0.25×), `P7_after_bridge.png`, `P7_after_tense.png`.

- **Sin Mixamo** (no hay `_incoming/mixamo/`): clips de **100STYLE** (CC BY 4.0) recortados con `Tools/Blender/mine_100style.py` (busca arranques, frenadas, giros y pivotes en la trayectoria de la cadera, dentro del rango válido de `Frame_Cuts.csv`) y convertidos con `Tools/Blender/convert_100style.py`. Importados como Humanoid con mapa de huesos explícito (`Style100Import`); la rotación del 4.º hueso de columna se suma al 3.º en Blender para que Unity no la descarte (0 advertencias de importación).
- **Arranque y frenadas** (probado): `WalkStart`, `WalkStop`, `RunStop`; mientras duran, el avance sale del root motion del clip (`PlayerController.DrivenSpeed`), así los pies no patinan. Frenada caminando: 0.32 m. Arranque al trote o al sprint: inclinación (la alternativa del spec), no hay clip limpio.
- **Pivote** (probado): media vuelta corriendo → clip `Pivot`; el giro lo sigue haciendo el controlador a 540°/s.
- **Giro en el sitio** (probado): parado y con > 70° pedidos, el controlador gira en 0.45 s (90°) a 0.65 s (180°) sin avanzar (0.00 m) mientras el clip da los pasos hacia ese lado (`TurnLeft`/`TurnRight`, lado detectado por la velocidad angular de la raíz).
- **Inclinación en curvas** (probado): hacia dentro, proporcional a la velocidad de giro, máx. 7° (medido 4.4° trotando en círculo).
- **IK de pies** (probado en el puente): tobillo a 0.2 cm de su altura sobre la tabla y punta del pie apoyada (0.2 cm); la cadera baja 5 cm. Rayo al suelo por pie, alineado con la normal, apagado en el aire del paso.
- **Idle con vida** (probado): tras 6 s quieto mira atrás (`LookAround`, estilo "Followed"); con un Lurker a ≤ 14 m, idle tenso encorvado (estilo "BentForward").
- Velocidades naturales de los clips UAL re-medidas en el papá nuevo: 0.91 / 2.51 / 3.53 / 0.67 m/s (tabla: 0.93 / 2.60 / 3.62 / 0.69); diferencia ≤ 3.5%, las escalas siguen dentro de 0.85–1.1×.
- Revisión a 0.25× (tiras): el arranque y la frenada se ven sin patinar; en la tira del arranque el papá se agacha solo al rozar la hierba (sigilo automático de P1, correcto).
- `UNVERIFIED`: los 60 s de juego seguidos a 0.25× de la sección 4 (se revisaron las transiciones nuevas por separado, no una partida entera); IK de mano izquierda con Animation Rigging (la linterna sigue con la capa de brazo de UAL, sin el paquete); recoger con IK de mano al objeto y alineación de 0.2 s antes de registrar/usar la radio; clip de golpe de bate (sigue el de espada de UAL); convulsión del Lurker aturdido (sigue la capa procedural).

## 2026-10-21 — POLISH PASS P6: personajes nuevos (papá MakeHuman, Lurker húmedo)
Editor, play mode vía Unity MCP. Prueba: `PolishTests.Run("P6")` → **5/5 PASS**; regresión `PolishTests.Run("P3")` → **6/6 PASS** (papá 10.0–11.5% del alto, sombra 55%, luces 3.4%). Capturas: `P6_before.png`, `P6_before_pose.png`, `P6_after.png`, `P6_after_pose.png`, `P6_after_pose_front.png`, `P6_after_lurker.png`, `P6_after_lurker_side.png`, `compare_P6.png`.

- **Ruta A funcionó** (sin pasar a la B): Blender 4.2.9 portable + MPFB 2.0.18 en modo headless. `Tools/Blender/make_father.py` genera al papá (38 años, complexión media, pelo corto, barba de tres días, chaqueta de trabajo + camisa + jeans de `male_casualsuit05`, botines `toigo_ankle_boots_male`), rig `game_engine`, texturas a 1k y lo exporta a `Assets/ThirdParty/MakeHuman/Father/`. **14 462 triángulos** (≤ 15k), una malla con 6 submallas (un solo SkinnedMeshRenderer).
- **Mismas animaciones** (probado): Humanoid, mismo `Father.controller` y clips UAL; caminando los pies se cruzan ±0.8 m a lo largo del avance (el retarget funciona).
- **Materiales** (probado): piel con SSS falso (emisión = difuso teñido de rojo; ojo: con `globalIlluminationFlags = None` Unity quitaba `_EMISSION`), ropa con normal + AO, botas mojadas (suavidad 0.62), barro pintado por el script en el bajo de los jeans y las botas, pelo y cejas con recorte alfa. Mochila con el garrote enfundado (sigue en la funda al caminar, probado en juego).
- `Mat()` del generador ahora limpia mapas y palabras clave de la versión anterior del asset (las botas arrastraban el mapa metálico del cuero viejo).
- **Lurker** (probado): piel oscura y mojada (suavidad 0.70), sin ropa (un material), brazos ×1.50/×1.40 (asimétrico), piernas ×1.20, cuello ×2.10, ojos emisivos de 14 cm (a escala 1.3).
- `Shots.PoseSheet`: la primera celda salía en pose de enlace (la primera evaluación del grafo no llega a los huesos); arreglado.
- **Comparación con la referencia** (`compare_P6.png`), 5 diferencias:
  1. Las terrazas del nivel se leen como losas oscuras y planas con borde recto (en la referencia el borde es roca y vegetación irregular). *Atacada en parte*: más rocas (huecos 20% → 10%) y matas en el 85% del borde (antes 55%); la losa sigue leyéndose → queda como la diferencia nº 1 para P9.
  2. Dominante verde azulada en todo el cuadro; la referencia es tierra y ámbar con el verde solo en los haces. *Atacada*: balance de blancos 14 → 20, sombras menos verdes, niebla casi neutra (P3 sigue 6/6).
  3. Lluvia en primer plano con gotas blancas gruesas que ensucian la lectura (la referencia no tiene). *Atacada*: alfa 0.32 → 0.18 y gotas más finas.
  4. El Lurker se lee como silueta negra; el de la referencia tiene brillos húmedos pálidos que dibujan la anatomía.
  5. El plano medio está vacío; la referencia tiene utilería narrativa (campamento, muelle, agua con reflejos) en cada plano.
- `UNVERIFIED`: rendimiento del nuevo papá en iPhone (14.5k tris + 6 materiales en un SkinnedMeshRenderer); calidad de la piel bajo la linterna de cerca (la cámara nunca se acerca tanto).

## 2026-10-21 — POLISH PASS P5: legibilidad de la IA sin conos pintados
Editor, play mode vía Unity MCP. Prueba: `PolishTests.Run("P5")` → **5/5 PASS**. Capturas: `P5_before.png` (cono rojo en el suelo), `P5_after_patrol.png`, `P5_after_detecting.png`, `P5_after_hunting.png`, `P5_after_edge_arrow.png`.

- **Sin cono en el suelo** (probado): `VisionCone` solo se dibuja con la opción "Ayudas de sigilo" del menú de pausa (apagada por defecto, se guarda en PlayerPrefs).
- **Ojos** (probado): brasa tenue en patrulla (0.6, 0.02, 0.02), naranja intenso que parpadea al investigar o buscar, rojo (6, 0.1, 0.05) con un haz leve desde los ojos al cazar (haz 1.00).
- **Cabeza** (probado): apunta a lo que atiende (el punto que investiga, el último lugar donde te vio o la bengala), hasta 75°; medido 21° hacia un punto a 90° del cuerpo mientras se gira.
- **Indicador de detección** (probado): arco sobre la cabeza que se llena de blanco a naranja (visto a 0.53) y queda lleno y rojo al cazar; solo con sospecha > 0.05. Fuera de cámara, flecha en el borde hacia el Lurker.
- **Sonido**: chasquido gutural al empezar a sospechar, chillido al detectar (ya existía) y respiración procedural audible a ≤ 8 m. `UNVERIFIED`: cómo suenan.
- Pendiente visto en las capturas: con la cámara a 42° el tramo bajo de los haces del OVNI se lee como un "balde" rojo sólido cerca del suelo (va a P9).

## 2026-10-20 — POLISH PASS P4: entorno (árboles, rocas, vegetación por densidad)
Editor, play mode vía Unity MCP. Prueba: `PolishTests.Run("P4")` → **5/5 PASS**; `PolishTests.Run("P3")` repetida con el bosque nuevo → **6/6 PASS**. Bot `LoopBot.Run(24)` → llega al final de la sala 3, 2 muertes (antes 3). Capturas: `P4_before*.png`, `P4_after*.png`, cenital `P4_after_topdown.png`, mapa `P4_after_density.png`.

- **Árboles**: coníferas con tarjetas de ramas (atlas `fir_tree_01`), copa verde casi negro, tronco libre bajo la copa (1.1–2.6 m visibles desde la cámara a 42°), 4 variantes + árbol muerto, impostor (dos tarjetas cruzadas horneadas) por LOD a ~30 m. Ya no hay "blobs" estilizados: los arbustos del borde son matas de helecho Poly Haven.
- **Rocas**: `rock_moss_set_01/02` reducidas a ≤ 3k triángulos (la mayor: 2954) con el shader `MossyRock` (musgo arriba por normal, cortado por ruido). Colisionadores convexos con mallas de 200 triángulos (sin avisos de 256 polígonos). Tocones y troncos muertos Poly Haven reducidos (≤ 2.5k / 3.5k).
- **Vegetación escondite por densidad**: `HideDensityMap` (celdas de 0.25 m) = escondites de diseño deformados (elipses con borde ondulado) + ruido junto a rocas, bordes y orillas. 23 masas de diseño, ninguna circular (irregularidad mínima 0.11; un círculo en esta rejilla da ~0.03). Dentro de lo alcanzable, lo procedural nunca esconde (solo los escondites medidos contra las patrullas). El escondite se calcula muestreando el mapa: centro de una masa → densidad 1.00, oculto; borde fino → 0.24, visible.
- **Ramas secas** claras en 12 atajos: pisarlas caminando hace 7.5 m de ruido (caminar 3.5 + 4) con crujido de madera.
- **Hitos**: árbol muerto gigante al fondo del prado de los reflectores; restos humeantes con brasas en la terraza de la espesura (isla de luz cálida sin luz dinámica). El refugio, el puente y la camioneta ya estaban.
- Exposición del grading 3.7 → 4.1 (el bosque nuevo es más oscuro); histograma en juego: sombra profunda 55%, luces 3.4%.
- `UNVERIFIED`: rendimiento en iPhone (4.9k matas de pasto combinadas por parcelas de 12 m, 520 coníferas con impostor, MossyRock); el aspecto del impostor de cerca no se revisó (cambia a ~30 m); charcos con reflejo sin hacer (siguen desactivados); suelo con 4 capas pintadas (sin capa de musgo nueva).

## 2026-10-20 — POLISH PASS P3: cámara, luz y post-proceso
Editor, play mode vía Unity MCP. Prueba: `PolishTests.Run("P3")` → **5/6 PASS** (falla el 3% de luces del histograma, ver abajo). Capturas: `P3_before.png`, `P3_after.png`, `P3_after_0..4.png`, `P3_after_dither.png`; comparación `docs/progress/compare_P3.png`.

- **Escala del papá** (probado): de pie ocupa 9.7–11.4% del alto de la pantalla en 5 salas (antes ~16% a 12.5 m). Cámara: 42°, FOV 35, distancia 17.5 m (ver DECISIONS).
- **Mirar adelante** (probado): corriendo, el encuadre se adelanta 1.44 m (objetivo 1.5, suavizado 0.4 s).
- **Amenaza** (probado): con un Lurker en Chase la cámara pasa de 16.6 a 19.0 m y encuadra entre el papá y la amenaza (peso 0.3).
- **Tilt-shift** con el plano focal en el papá (probado: foco 0.42 = altura del papá en pantalla).
- **Oclusión con dithering** (probado): un árbol que tapa al papá pasa a `ProjectRidge/DitherFade` y se perfora (alfa 0.22) en vez de volverse transparente; sigue proyectando sombra. Todos los árboles del lado de la cámara son desvanecibles.
- **Luz**: luna baja a contraluz (desde el fondo), ambiente 40% menos y sin tinte azul, linterna cálida 3200 K de 14 m con cookie, sombras y borde suave; niebla de fondo más leve. Faroles cálidos (emisivos) con charco de luz naranja en cada poste del sendero, sin luces dinámicas nuevas. Borde de los haces del OVNI fino y parpadeante con caída suave hacia adentro, polvo suspendido dentro de cada haz.
- **Post**: ACES, contraste 18, exposición 3.7, sombras verde azulado oscuro, luces altas cálidas, balance algo cálido, grano y viñeta. Se quitó un `lift` de +0.01 que subía todo el negro a ~0.1 en pantalla (por eso antes había 0% de sombra profunda).
- **Histograma** (media de 5 capturas de juego): sombra profunda (< 0.08) **58%** ✓ (antes 0%); islas de luz (> 0.6) **2.4%** ✗ (objetivo 3%). Las salas con haces del OVNI pasan (3.0–4.5%); las de entrada y el pasillo del arroyo no tienen fuentes de luz fuertes. Con los charcos de farol amarillos llegaba a 3.3%, pero se veían verdosos; se prefirió el naranja correcto. Queda para P4 (hito con luz en cada sala).
- **Comparación con la referencia — 5 diferencias**: (1) paleta azul fría frente a tierra y musgo cálidos → **atacada** (balance cálido, ambiente y niebla neutros, luna menos saturada); (2) charcos de farol blanquecinos/amarillos → **atacada** (naranja HDR con alfa < 1); (3) agua como una franja plateada → **atacada** (suavidad 0.95 → 0.62, color más oscuro; sigue brillante con la luna a contraluz); (4) árboles "blob" azules sin tronco → P4; (5) suelo liso sin textura densa (pasto, hojarasca, raíces) → P4.
- `UNVERIFIED`: el desvanecido por dithering en iPhone (ancho de patrón en pantallas Retina); el coste del polvo de los haces en móvil.

## 2026-10-20 — POLISH PASS P2: captura del OVNI con peso (bloqueo, parálisis, forcejeo, elevación)
Editor, play mode vía Unity MCP. Prueba: `PolishTests.Run("P2")` → **5/5 PASS**. Secuencia en 8 capturas: `docs/progress/P2_1_exposure` … `P2_8_flash` (exposición, bloqueo, parálisis, forcejeo fallido, elevación, forcejeo con éxito, soltarse, destello).

- **Bloqueo** (probado): el haz se detiene en seco (deriva medida 0.000 m durante bloqueo y parálisis), se estrecha al 70% y pasa a blanco frío; el papá no se mueve (0.000 m), CharacterController apagado, entrada ignorada.
- **Parálisis** 1.6 s: pose de shock procedural (`ShockPose`: brazos separados, dedos abiertos, cabeza atrás) con temblor de 18–25 Hz que sube de 1.5° a 4°, cuerpo congelado, linterna que falla, aberración cromática + grano + saturación al 0.3 + viñeta fría, cámara que se acerca 12%, crepitar eléctrico procedural, latido cortado, zumbido más agudo.
- **Forcejeo** (probado): anillo sobre el papá; 2 toques no bastan → elevación 3.0 m, destello blanco, negro y checkpoint; 14 toques → se suelta, rueda (clip Roll), el haz retrocede 4 m y queda ciego 2.5 s, se sigue jugando. Solo una vez por vida (la segunda captura en la misma vida va directo a la elevación; al reaparecer vuelve a estar disponible).
- Mientras se llena la exposición: viñeta fría proporcional y zumbido que sube de tono.
- Hápticos: API `Haptics` + plugin mínimo `Assets/Plugins/iOS/RidgeHaptics.mm` (UIImpactFeedbackGenerator, zumbido a 25 Hz durante la captura). `UNVERIFIED`: no hay iPhone ni Mac aquí; no se compiló para iOS.
- `UNVERIFIED`: cómo suena el crepitar; que el forcejeo con toques sea cómodo en táctil real (en la prueba se usaron pulsaciones de teclado virtuales); el vaivén del joystick como alternativa no se probó.

## 2026-10-20 — POLISH PASS P1: controles táctiles, sigilo automático, armas de supervivencia
Editor, play mode vía Unity MCP. Prueba: `PolishTests.Run("P1")` → **13/13 PASS**. Recorrido: `LoopBot.Run(24)` (salas 1–3 solo con joystick + ACCIÓN + OBJETO virtuales) → llegó al final de la sala 3 en 136 s con **3 muertes** (salida del puente (-8.8, -0.6), pasillo del arroyo (-1.7, -0.2) y final de la sala 3 (21.5, -0.5)); 3 pulsaciones de ACCIÓN y 2 de OBJETO. Capturas: `P1_before.png`, `P1_after.png` (HUD táctil), `P1_after_hidden.png`.

- **Táctil (3.1)**: joystick flotante en el 45% izquierdo (solo un anillo tenue; arrastrar más allá del anillo fija el sprint y el anillo se ilumina), botón ACCIÓN (110 pt) y OBJETO (84 pt), linterna y pausa arriba a la derecha, curar manteniendo 0.6 s el indicador de vida. 0 botones de agacharse o sprint (probado sobre los OnScreenButton de la escena).
- **Inclinación del stick** (probado): 0.25 → sigiloso de pie 0.80 m/s; 0.6 → caminar 1.78 m/s (mezcla continua caminar→trotar); 0.9 → trote 2.60 m/s; al borde 0.25 s → sprint 3.60 m/s; al aflojar se suelta. Teclado: WASD trota (no fija sprint), Shift esprinta.
- **Sigilo automático (3.2)** (probado): en el pastizal se agacha solo y queda oculto; fuera, sigue agachado 0.2 s y se levanta antes de 0.8 s (histéresis 0.4 s); junto a un tronco con un Lurker cerca se agacha tras la cobertura; con amenaza y el stick apenas inclinado camina agachado; al trotar se levanta. Oculto = viñeta, paso bajo en el audio y silueta oscura del papá (sin texto). `UNVERIFIED`: cómo se siente la viñeta y el filtro de audio.
- **ACCIÓN contextual** (probado): inspeccionar › usar › recoger › registrar › derribo › golpe › empujar. Derribo por la espalda agachado: el Lurker cae (estado Down, 20 s) y se levanta buscando; gasta 1 de durabilidad (4 → 3). Golpe: aturde 2.5 s y empuja. Ningún Lurker muere (la vida no baja de 1).
- **OBJETO** (probado): piedra al objetivo inteligente (cae 3 m más allá del Lurker); Tab cambia a la bengala; la pistola de bengalas dispara (arde 8 s, ruido 18 m). Topes: piedras 3, bengalas 2, botiquines 2. En el nivel: 3 bengalas y 3 botiquines.
- Ruido: sigiloso/agachado 1 m, caminar 3.5, trotar 7, correr 12.
- `UNVERIFIED`: apuntar arrastrando y deslizar para cambiar de objeto en una pantalla táctil real (solo se probó el toque por teclado/control virtual); tamaños de botón en un iPhone real; hápticos (pasan a P8).
- Las muertes del bot en el arroyo vienen de antes (ver la sección del pase anterior); se revisan en P9.

## 2026-10-20 — POLISH PASS P0: arreglos rápidos (D1–D4, D14, sin chatarra)
Máquina: Windows 11 (RTX 4070), Unity 6000.3.26f1, editor, play mode vía Unity MCP. Prueba: `PolishTests.Run("P0")` → **8/8 PASS**. Capturas: `docs/progress/P0_before.png`, `P0_after.png` (corriendo, garrote en la espalda), `P0_after_pickup.png`.

- Compila: 0 errores, 0 warnings propios (se corrigió el `compressLightmaps` obsoleto). Consola en play: 0 errores.
- **D1 velocidades de los clips**: medidas por la zancada (el pie apoyado se desliza hacia atrás a la velocidad del cuerpo; `CharacterBuilder.MeasureNaturalSpeed`): caminar 0.93, trote 2.60, sprint 3.62, agachado 0.69 m/s; Lurker (escala 1.3) caminar 1.15, trote 3.34, sprint 4.60. Config: agachado 0.72, caminar 1.0, trote 2.6, sprint 3.6. Escala de los clips en los blend trees: 0.99–1.10x (antes 1.26–1.47x). Persecución del Lurker 4.4 → 3.9 m/s para mantener la proporción con el sprint nuevo (1.08x; antes 1.05x).
- **D2 giro**: 540°/s; media vuelta medida en 0.32 s (antes 0.1 s).
- **D3 recoger**: clip de agacharse al suelo (Farm_Harvest, UAL2) en lugar de PickUp_Table. Probado: entra en el estado PickUp y la cadera baja 0.20 m. `UNVERIFIED`: la mano no llega al objeto por IK ni el objeto se pega a la mano (queda para P7).
- **D4 garrote enfundado**: va cruzado en la mochila; se desenfunda solo con un Lurker cazando a ≤ 6 m o al golpear (la mano va a la funda por IK, 0.35 s) y se enfunda 3 s después. Probado: corriendo → en la funda; Lurker en Chase → en la mano; 4.5 s después → en la funda. El golpe sigue usando Sword_Regular_A (se cambia en P7).
- **D14**: los botones de fin nunca se ven en el título (alfa 0 desde el generador y al arrancar).
- **Chatarra y fabricación eliminadas** (item, recogibles, cajas, HUD, animación). Sin garrote, ATTACK no hace nada (P1 añade empujar/esquivar).
- 100STYLE (CC BY 4.0) descargado y verificado (md5) en `_incoming/100style/` (fuera del repo). Aún no entra a `Assets/` (ver DECISIONS.md).

## 2026-10-20 — Pase de feedback anterior (estilo salvaje, bengala de mano, peso del combate)
Commits `1ba7a37` y `46a1a43`. Pruebas `FeelTests` 12/12 PASS antes de los últimos cambios de luz y agua.
- `UNVERIFIED`: el aspecto del agua en movimiento; el arreglo del disco blanco en la toma exacta del usuario (verificado solo cerca de la caja del refugio); justicia de la sala del arroyo (el `LoopBot` muere en (-10.5, -4.3)); cómo suenan las pisadas Kenney y la voz grave del Lurker; rendimiento en dispositivo.

## 2026-10-19 — Nivel compacto, personajes Quaternius, eventos con guion, atmósfera y perfil iPhone (A18+)
Máquina: Windows 11 (RTX 4070), Unity 6000.3.26f1, editor (play mode vía Unity MCP). Escena: `TheHollow.unity`, regenerable con `Project Ridge/The Hollow/Build All`.

- Compila: 0 errores. Consola en play tras Build All: 0 errores.
- **Personajes** (Quaternius CC0, ver ASSETS.md): papá con ropa por regiones del esqueleto, pelo/barba, linterna y garrote; Lurker deformado (alargado y adelgazado) con capa procedural de tics. Verificado en capturas en play: dedos hacia adelante al caminar/trotar, agachado, capa de antorcha, garrote en la mano.
- **Piedra de distracción** (G / LB / THROW): parábola con ápice fijo hacia el suelo real del destino (salva bordes). Probado: la piedra choca con la meseta si el jugador lanza contra el muro de 4 m (correcto); al caer junto al Lurker B: Patrol → Investigate → camina al punto → Search.
- **Clímax en la radio**: 25 s de transmisión, cuenta en el objetivo, ruido cada 6 s, OVNI barriendo alrededor de la camioneta. Probado: con los Lurker activos y el papá quieto, el Lurker A llega y lo mata (esperado); sin Lurkers, al terminar → `Ending` y texto final. Corregido: si el flujo sale de Playing a mitad de la transmisión, la radio quedaba bloqueada; ahora se puede reintentar.
- **Respawn**: los Lurker cercanos vuelven al punto de su ruta más lejano al checkpoint y no ven ni oyen durante 6 s (antes había bucle de muerte junto a la camioneta). Probado: el Lurker A reaparece en (16.5, 4.5) y el papá revive sin ataque inmediato.
- **Aparición con guion**: el Lurker cruza la otra orilla y se detiene a mirar al papá la primera vez que queda bien en cuadro a 5–13 m; luego huye. Probado: se detuvo en el viewport (0.29, 0.77) a 8.5 m, de frente al papá (dot 1.00), y desapareció al final del recorrido. `UNVERIFIED`: que se lea bien en pantalla (en la captura queda muy oscuro entre rocas).
- **Haz del OVNI**: volumen analítico (intersección rayo-cono recortada por profundidad, 10 pasos), solo caras traseras: sin bordes duros ni "pared" cerca de la cámara. El OVNI se proyecta sobre el terreno (antes saltaba a las copas, y el haz terminaba a 13 m del suelo). Altura 20 m, desplazamiento 15 m (la nave nunca cruza el encuadre). Verificado en captura en el prado.
- **Luz y gradación**: cubemap de reflejo nocturno propio (los charcos reflejaban un cielo brillante); exposición 2.15 y contraste 12. Luminancia media de captura: antes 0.02–0.05 (85–97 % casi negro), ahora ~0.2 con bordes oscuros. Linterna 16/18 m. Dial ámbar emisivo en la camioneta como guía hacia la meta (sin gastar luz). Lluvia nace por debajo de la cámara.
- **Perfil iPhone** (`Mobile` = calidad por defecto de iPhone, `Mobile_RPAsset`): escala 0.8 con FSR + nitidez 0.85, sombras de luna 28 m / 2 cascadas / 2048, SSAO igual que PC a media resolución (antes tenía los valores por defecto: intensidad 3, radio 0.035, otro look), Forward con todas las variantes de la vegetación. Verificado en el editor forzando `Mobile_RPAsset` en play: mismo look que PC; 616 batches, ~990 draws, 541k tris, 559 shadow casters en el puente.
- Medido en el editor con el perfil PC en 6 puntos del nivel: 457–688 batches, 617–1130 draws, 342–591k tris, ~100 SetPass (SRP Batcher), 338–739 shadow casters, render 4–5 ms en la RTX 4070.
- Orientación: quitado el retrato (estaba permitido); solo horizontal izquierda/derecha.
- Build Windows `Builds/Windows/ProjectRidge.exe` (181 MB, 0 errores, 0 warnings): arrancado 35 s; log del player sin excepciones, ambos Lurkers en el NavMesh.
- `UNVERIFIED`: rendimiento real en iPhone 16/17/18 y iPad M (no hay dispositivo ni Mac aquí). Riesgos a vigilar en el primer build: ~1000 draws (árboles con viento no se combinan), 3 luces con sombra (luna, linterna y reflector del OVNI; CLAUDE.md pide solo luna y linterna, pero las sombras del reflector son las que muestran dónde esconderse — decisión pendiente).
- `UNVERIFIED`: cómo suena el audio nuevo (lluvia, arroyo, radio, piedra).

## 2026-10-19 — The Hollow completo (M4–M7 en PoC): combate, nivel, personajes, flujo, audio
Máquina: Windows 11, Unity 6000.3.26f1, editor (play mode vía Unity MCP). Escena: `Assets/_Project/Scenes/TheHollow.unity` (primera y única del build). Regenerable con `Project Ridge/The Hollow/Build All`.

- Compila: 0 errores. Consola en play: 0 errores, 0 warnings.
- **Combate** (probado en play con teclado y gamepad virtuales):
  - Garrote (clic izq / RT / ATTACK): golpe por sorpresa agachado por la espalda 68 de daño (el Lurker siguió en Patrol hasta el golpe); golpe normal 34 + aturdimiento; durabilidad 6 → 5; un golpe por enemigo por swing.
  - Bengala (Q / RB / FLARE, o apuntar + atacar): disparada cae y arde con luz roja (sin sombras) y ruido; el disparo atrae al Lurker. A quemarropa: 40 de daño + aturdimiento; con eso el Lurker murió (estado Dead, fuera de la lista de activos).
  - Sin bengalas → "No flares". Sin garrote, Attack fabrica uno con 2 de chatarra (`UNVERIFIED` en play: la rotura del garrote no se forzó en prueba).
- **Nivel** (capturas revisadas): refugio sin techo con farol (única luz local), sendero, arroyo en barranco con puente y tronco caído, prado del OVNI con rocas y pastizales, bosque norte denso, campamento abandonado, camino con la camioneta. 300 árboles chunky, 267 rocas, 95 arbustos, ~2700 matas de pasto, 11 pastizales altos (TallGrass + sombra frente al OVNI).
- Colisiones probadas: el barranco no se puede bajar fuera de los cruces; el puente y el tronco caído se cruzan; desviándose en el tronco o el puente el jugador queda en la orilla (no cae); el acantilado de la meseta bloquea.
- **Flujo**: título → juego al pulsar; pista de la foto (pausa el juego, objetivo "Follow the trail east."); checkpoint del puente ("CHECKPOINT", objetivo nuevo); muerte → "YOU DIED" → reaparece en el checkpoint con vida llena; radio de la camioneta → secuencia final con texto. Probado en play.
- Captura del OVNI probada en play: de pie en el prado bajo el reflector → "TAKEN", el papá sube ~4 m en la luz y reaparece en el último checkpoint.
- Build Windows `Builds/Windows/ProjectRidge.exe` (168 MB, 0 errores): arrancado 30 s; log del player sin excepciones y ambos Lurkers en el NavMesh (antes fallaba: "Failed to create agent", corregido).
- **Personajes**: papá y Lurker procedurales (rigs de articulaciones + animación por código). Verificado en capturas en play: caminar/trotar, agachado, golpe (arranque y recuperación), muerte del papá; Lurker encorvado patrullando, ataque, aturdido (convulsión).
- **Audio**: 43 clips sintetizados en el proyecto (sin terceros), sin clipping (verificado leyendo los WAV). AudioDirector con música calma/persecución, latido, pasos (tierra/madera), voces del Lurker, UI.
- `UNVERIFIED`: cómo suena todo (no hay forma de escucharlo desde aquí); mezcla de volúmenes.
- `UNVERIFIED`: rendimiento en iPhone/iPad; táctil en dispositivo; iluminación horneada (no se horneó).

## 2026-10-19 — Recursos e interacción (M4 parcial) en LookDev_ForestCorner
Máquina: Windows 11, Unity 6000.3.26f1, editor (play mode vía Unity MCP).

- Compila: 0 errores. Consola en play: 0 errores (solo el warning conocido de `Account API`).
- Nuevo: `ItemDefinition` (Medkit máx 3, Flare máx 3, Scrap máx 9), `InteractionConfig`, `PlayerInventory`, `PlayerInteractor`, `PlayerHealing`, `Pickup`, `SearchableContainer`, `SurvivalHud`. Acción Heal (H / cruceta arriba / botón táctil HEAL). Herramienta `Project Ridge/Items/Setup Items in Active Scene`.
- Escena: 6 objetos sueltos (2 botiquines, 1 bengala, 3 chatarra) y 2 cajas (inicio y claro).
- Probado en play con teclado virtual por el Input System:
  - E junto a un botiquín → inventario 1, objeto desaparece.
  - H con 40 de vida → 90 y botiquín consumido; H sin botiquines → mensaje "No medkits".
  - E en la caja del inicio → +1 Medkit +2 Scrap, tapa abierta, mensaje en el HUD.
  - HUD (captura en play): barra de vida, contador de recursos, aviso "[E] Pick up Scrap", destello rojo al recibir daño, botón HEAL.
- Arreglado: `EnemyStateFeedback` lanzaba `ArgumentNullException` cada frame tras una recarga de scripts en play mode.
- Arreglado: E en el mismo frame en que el jugador llega junto al objeto no hacía nada; ahora el interactor busca el más cercano al pulsar.
- Verificado (pendiente desde la entrada anterior): agachado detrás de la roca el rayo del OVNI no lo ilumina; de pie en el mismo lugar y agachado en campo abierto, sí.
- `UNVERIFIED`: que el ruido de recoger/registrar atraiga al Lurker en partida real (usa `NoiseSystem`, probado en M2 con otros ruidos).
- `UNVERIFIED`: lectura de los objetos en la oscuridad a escala de iPhone.

## 2026-10-17 — Look dev (esquina del bosque) + OVNI (M3 parcial) + HUD + playable Windows
Máquina: Windows 11, Unity 6000.3.26f1. Escena: `Assets/_Project/Scenes/LookDev_ForestCorner.unity` (primera del build).

- Compila: 0 errores. Herramientas de editor regenerables en el menú `Project Ridge/Look Dev` y `Project Ridge/HUD`.
- Arte: terreno con 4 capas Poly Haven (CC0), 130 coníferas procedurales (570–850 tris) con el atlas de ramitas de `fir_tree_01`, rocas, roca grande con LODs, helechos, ramas, tronco y tocones. Luna Mixed, niebla, post-proceso (ACES, color, bloom, viñeta).
- Cámara: pitch 58°, distancia 18 m, FOV 45°. Los árboles y rocas que tapan al jugador se desvanecen (probado en play: el jugador se ve a través del árbol).
- Probado en play: el Lurker patrulla el claro sin emboscar el inicio (25 s después del inicio sigue a 23.6 m); respawn a los 2 s con vida llena.
- Build Windows: `Builds/Windows/ProjectRidge.exe` generado con éxito (182 MB, 0 errores). **Ese build es anterior al OVNI y al HUD nuevo.**
- OVNI: no se ve; solo haz (luz spot con sombras + cono aditivo) y zumbido sintetizado 3D. Escanea el claro, te sigue al detectarte y alerta al Lurker. Exposición por raycast desde la luz (rocas, terreno y copas tapan).
  - Probado: en campo abierto se llena la exposición → HUNTED, el OVNI te sigue y el Lurker va a tu posición.
  - Problema encontrado y corregido en código: con el haz a 52° y midiendo la cabeza, agachado detrás de la roca grande seguías iluminado. Ahora el haz va a ~37° y agachado solo cuenta el pecho. `UNVERIFIED`: repetir la prueba de escondite tras el cambio.
  - Corregido en código: el Lurker se quedaba atacando en el punto de respawn; ahora al reaparecer olvida al jugador y vuelve a su ruta si estaba cerca. `UNVERIFIED` en play.
- HUD: controles táctiles circulares y discretos (solo móvil/editor), indicador SAFE / ALERT / HUNTED con barra. `UNVERIFIED`: aspecto en pantalla (no hay captura del HUD), zumbido audible (no se puede oír desde aquí).
- Rendimiento (editor, sin Profiler de dispositivo): ~808k triángulos y ~125 batches en la vista general de la escena. Demasiado para un A15: las rocas de Poly Haven (roca grande 66k, tronco 100k, tocones 41k) son lo más caro. Pendiente: reemplazar o reducir.
- Pendiente: hornear la iluminación (configurada, no ejecutada).

## 2026-10-17 — M2: ruido + percepción + IA del Lurker (Sandbox_Greybox)
Máquina: Windows 11, Unity 6000.3.26f1, editor (play mode vía Unity MCP). Sin paquetes nuevos (AI Navigation 2.0.15 ya estaba).

- Compila: 0 errores. Consola en play mode: 0 errores (solo el warning conocido de `Account API`).
- Sistemas: `NoiseSystem` (bus estático de `NoiseEvent`), `PlayerNoiseEmitter`, `PlayerVisibility`, `EnemyPerception`, `EnemyController` (Patrol → Investigate → Search → Chase → Attack), `EnemyStateFeedback` (ojos), `NoiseDebugView` (anillos), `PlayerHealth` mínimo, linterna (F / Y). Valores en `NoiseConfig`, `StealthConfig`, `EnemyConfig`, `PlayerHealthConfig`.
- NavMesh horneado con `NavMeshSurface` (solo capa Default).
- Probado en play mode:
  - Patrulla: recorre su ruta a 1.2 m/s y vuelve a ella después de buscar.
  - Oído: a 9 m no oye un paso caminando (3.5 m) ni trotando (7 m); un paso de sprint (12 m) lo manda a Investigate. Se queda quieto, va a la zona aproximada del ruido, busca 10 s y vuelve a Patrol.
  - Pasos reales: sprint emite radio 12 m cada 0.236 s; agachado, radio 1.2 m cada 0.33 s.
  - Vista: detrás de `Wall_Test`, a 9 m y dentro del cono, no lo ve (sospecha 0). Despejado a 12.7 m lo ve y la sospecha llega a 1.
  - Visibilidad (multiplicador de la ganancia de sospecha): de pie quieto 0.75, agachado 0.41, agachado en pasto 0.19 (nunca 0), con linterna 1.2.
  - Chase → Attack: lo persigue, rodea la pared y ataca; exactamente un golpe de 35 por ataque, cada 1.60 s.
  - Pierde el rastro: sin verlo pasa a Search en la última posición conocida; la sospecha decae a 0.
  - Navegación: la ruta rodea `Wall_Test` (14.7 m en vez de 11 m en línea recta) y sube a la plataforma por la rampa.
- No probado aún: zig-zag y quietud vistos en pantalla (solo comprobados en código y estados); feedback visual de los ojos.
- Pendiente para M4: al llegar a 0 de vida no hay muerte ni checkpoint (el Lurker sigue atacando sin efecto).

## 2026-10-17 — M1: movimiento + cámara + input (Sandbox_Greybox)
Máquina: Windows 11, Unity 6000.3.26f1, editor (play mode vía Unity MCP). Paquete nuevo: `com.unity.cinemachine` 3.1.7 (+ `com.unity.splines` 2.9.1).

- Compila: 0 errores. Consola en play mode: 0 errores (solo el warning conocido de `Account API`).
- Escena `Assets/_Project/Scenes/Sandbox_Greybox.unity` (primera en Build Settings): suelo 60×60 con muros perimetrales, bloques, rampa de 12.8°, plataforma de 1 m, pasto alto (triggers en capa `TallGrass`), luna direccional Mixed con sombras, niebla.
- Input: `ProjectRidgeInput.inputactions` es ahora el asset project-wide (reemplaza al `InputSystem_Actions` del template, que queda sin usar).
- Probado en play mode con dispositivos virtuales del Input System (gamepad, teclado y mouse simulados):
  - Walk 2.50 / Jog 4.00 / Sprint 5.50 / Crouch 1.50 m/s, iguales a `PlayerMovementConfig`. Umbral del stick: 0.5 → Walk, 1.0 → Jog.
  - Movimiento relativo a la cámara y giro hacia la dirección de avance.
  - Sprint: Shift se mantiene (teclado); L3 y botón táctil alternan, y se cancela al detenerse o agacharse. Agachado no hay sprint.
  - Crouch (toggle): altura 1.80 → 1.10 y vuelta.
  - Los 16 bindings de botones (teclado, mouse, control) disparan su acción.
  - Colisión: no atraviesa `Wall_Test` (ni con sprint), ni `Rock_Tall` rotada, ni el escalón de 1 m; sube la rampa a la plataforma; no se sale de los muros perimetrales; rodea esquinas sin quedarse pegado.
  - Pasto alto: `IsInTallGrass` true dentro, false fuera.
  - Cámara: pitch 50°, yaw 45°, distancia 14 m, según `IsoCameraConfig`.
  - Controles en pantalla: con eventos de puntero simulados en el editor, el stick llega a `Move (1,0)` y los 5 botones a sus acciones.
- `UNVERIFIED`: táctil hasta probar en dispositivo (iPhone/iPad), incluida la safe area real.
- `UNVERIFIED`: control físico (MFi/PS/Xbox); solo se probó con un gamepad virtual.
- `UNVERIFIED`: aspecto visual de la escena desde la cámara de juego (la captura por MCP falló).
- No probado: levantarse bajo un techo bajo (no hay techo en el sandbox).
- Nota de pruebas: para correr play mode con el editor sin foco se activó `runInBackground` temporalmente y se restauró a `false`.

## 2026-10-17 — Unity MCP (`com.unity.ai.assistant` 2.20.0-pre.2)
Máquina: Windows 11, Unity 6000.3.26f1.

- Compila: el paquete importa y compila sin errores (≈39 s de compilación de scripts).
- Relay instalado por el editor en `~/.unity/relay/relay_win.exe`; registrado en Claude Code con scope `local` (`claude mcp add --scope local unity-mcp -- <relay> --mcp`).
- Conexión de Claude Code aprobada en Project Settings › AI › Unity MCP. Sin prueba, suscripción, pago ni vínculo a Unity Cloud.
- Probado: `Unity_RunCommand` creó `TestCube` (Cube, BoxCollider) en (0, 1, 0) en `SampleScene`; verificado leyendo la escena por separado. Escena no guardada.
- Consola: 0 errores, 2 warnings conocidos:
  - `Account API did not become accessible within 30 seconds` — asistente integrado esperando un proyecto de Unity Cloud; no se usa.
  - `Failed to collect Windows signature for ...\claude.exe` — validación de identidad del cliente MCP; la conexión se aprobó igual.
- No usar las herramientas `Unity_AssetGeneration_*`: son generación con IA de Unity y consumen créditos.

## 2026-10-16 — Repo y herramientas de git
- Git LFS activo; `.gitattributes` aplica LFS a binarios.
- UnityYAMLMerge (Windows, 6000.3.26f1) configurado como merge driver `unityyamlmerge` y probado en repo desechable: resolvió cambios en líneas contiguas de un `.asset` que el merge de texto de git deja en conflicto.
- `UNVERIFIED`: ruta de UnityYAMLMerge en Mac (`/Applications/Unity/Hub/Editor/<ver>/Unity.app/Contents/Tools/UnityYAMLMerge`); no hay Mac para probarla.
