# BUILD_STATUS

Qué compila, qué se probó, dónde y cuándo. Lo no verificado va marcado `UNVERIFIED`.

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
