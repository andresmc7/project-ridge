# Decisiones tomadas durante el POLISH PASS

Registro de decisiones ambiguas resueltas en modo autónomo (ver `docs/POLISH_PASS.md`, sección 0). Formato: fecha — decisión — por qué.

- 2026-10-20 — Se descarga **100STYLE** (Mason, Starke, Komura 2022; Zenodo 8127870; **CC BY 4.0**) en `_incoming/100style/` solo el `100STYLE.zip` con los BVH originales (1.47 GB), no los datos etiquetados (14.7 GB). — Para animación solo hacen falta los BVH; los datos etiquetados son para entrenar redes.
- 2026-10-20 — `ProjectSettings.runInBackground` queda en 1 **sin commitear** mientras dure el pase. — Las pruebas autónomas en play mode necesitan que Unity siga corriendo sin foco; se restaura a 0 antes del build (P9).
- 2026-10-20 — La referencia `target_look.png` muestra a la familia; se ignora ese detalle. — `CLAUDE.md` fija que el PoC tiene un solo humano (el papá).
- 2026-10-20 — Velocidades naturales medidas por la zancada (el pie apoyado se desliza hacia atrás a la velocidad del cuerpo), no por root motion. — Los clips de Quaternius vienen "in place": no tienen desplazamiento de raíz que medir.
- 2026-10-20 — Persecución del Lurker 4.4 → 3.9 m/s e investigación 1.4 → 1.25 m/s. — El sprint del papá bajó a 3.6 m/s (velocidad natural del clip); así el Lurker sigue un 8% más rápido (antes 5%) y sus clips no pasan de 1.1x.
- 2026-10-20 — Recoger del suelo con `Farm_Harvest` (UAL2) a 1.6x. — Es el único clip disponible que se agacha hasta el suelo con rodillas flexionadas. Es un clip de acción, no de locomoción: la regla de 0.85–1.1x no aplica. Se reemplaza por "Picking Up" de Mixamo si llega.
- 2026-10-20 — El garrote se desenfunda con IK de la mano a la funda (arco de 0.35 s) en vez de un clip. — No hay clip de desenfundar en UAL1/UAL2; los de Mixamo ("Draw Sword") se usarán en P7 si llegan.
- 2026-10-20 — Sin garrote, ATTACK no hace nada hasta P1 (empujar/esquivar). — Se eliminó la fabricación con chatarra (sección 3.3).
- 2026-10-20 — Las reglas de armas y recursos de la sección 3.3 (aturdir sin matar, derribo, límites de inventario, pistola de bengalas) se implementan en P1, junto con el botón contextual. — Ninguna fase las nombra; P1 es donde el botón ACCIÓN/OBJETO las necesita.
- 2026-10-20 — 100STYLE (BVH) se usará en P7 tras convertir a FBX con Blender (P6 instala Blender). Estilos útiles: `Neutral`, `InTheDark`, `Followed`, `Crouched`, `StartStop`, `Rushed`, `LimpLeft` (papá) y `Zombie`, `Dinosaur` (Lurker). — Unity no importa BVH directamente.
- 2026-10-20 — OBJETO con la bengala dispara la **pistola de bengalas** (sección 3.3), no la bengala de mano del pase anterior. La bengala de mano (`PlayerFlareTool`/`HandFlare`) queda en el código sin botón. — POLISH_PASS manda sobre el pedido anterior y coincide con la decisión fija de CLAUDE.md (pistola de bengalas).
- 2026-10-20 — Los íconos de ACCIÓN y OBJETO son palabras cortas (LOOK, USE, TAKE, SEARCH, TAKE DOWN, STRIKE, SHOVE; STONE/FLARE) hasta P8. — La tipografía y los íconos del HUD son parte de P8 (sección 3.12).
- 2026-10-20 — Los tamaños en pt se convierten a unidades del canvas con 2.49 unidades/pt (canvas 1920×1080, mezcla 0.5, iPhone 852×393 pt a 3x). — El canvas usa resolución de referencia; los pt del spec son de iPhone.
- 2026-10-20 — Umbral de sprint al borde 0.98 medido después de la zona muerta del stick (0.925 físico = 1). — Con 0.95 un stick a 0.9 físico ya contaba como borde y el trote se volvía sprint.
- 2026-10-20 — Derribo por la espalda a ≤ 1.8 m (el spec no da distancia; 1.6 m fallaba con el Lurker caminando). Requiere garrote y estar agachado; se fuerza agachado con C solo en pruebas (en juego se agacha en vegetación, cobertura o sigilo con amenaza).
- 2026-10-20 — Objetivo inteligente: Lurker delante (140°) a ≤ 12 m, el que caza pesa el doble; la piedra cae 3 m más allá de él para alejarlo; sin Lurker, 8 m hacia donde mira. No hay "puntos de interés" marcados en el nivel todavía.
- 2026-10-20 — Los arneses de prueba no llaman `InputSystem.Update()`: con el editor en primer plano esos eventos se procesaban en el búfer del editor y el juego no los veía.
- 2026-10-20 — Pose de shock procedural (`ShockPose`) y crepitar eléctrico generado por código mientras no haya clip "Electrocuted" de Mixamo ni un sample CC0. — 3.4 lo permite ("o, mientras no exista, una pose procedural").
- 2026-10-20 — El plugin de hápticos de iOS entra ya en P2 (no en P8). — La captura lo usa; sin el plugin, el build de iOS fallaría al enlazar `RidgeHapticContinuous`.
- 2026-10-20 — El forcejeo cuenta cualquier tecla o botón y los toques de pantalla. — 3.4 pide "toques rápidos en cualquier parte"; en control y teclado, cualquier botón es lo equivalente.
- 2026-10-20 — Cámara a 17.5 m (no 12.5) con 42° y FOV 35. — Medido en juego: a 12.5 m el papá de pie ocupaba el 16% del alto; el spec manda "mídelo y ajusta" hasta 10–12%. La distancia de amenaza sube en la misma proporción (20 m).
- 2026-10-20 — Charcos de farol naranjas aunque el histograma quede en 2.4% de luces (objetivo 3%). — Amarillos llegaban a 3.3% pero se veían verdosos sobre el pasto; el número se recupera en P4 con los hitos de luz por sala.
- 2026-10-20 — Exposición 3.7 / contraste 18 elegidos midiendo el histograma (2.1 dejaba 88% de la imagen en negro casi sin medios tonos).
- 2026-10-20 — Los escondites de diseño se conservan (posición y área) pero con forma de masa irregular, y lo procedural no esconde en la zona alcanzable. — 3.9 pide masas por densidad; si el ruido creara escondites nuevos junto a las patrullas, rompería el balance medido (P5 de la sesión anterior: 1.9 m de las rutas).
- 2026-10-20 — Impostor de 2 tarjetas cruzadas horneadas de lado (no un impostor octaédrico). — Suficiente con la cámara fija a 42°; más barato de generar y de dibujar.
- 2026-10-20 — Musgo de las rocas con un shader propio simple (Lambert con wrap + normal map + SSAO), no URP Lit. — URP Lit no mezcla por normal; la roca es mate, el especular no se echa de menos.
- 2026-10-20 — Charcos con reflejo y capa de suelo de musgo quedan sin hacer en P4. — Prioridad a árboles, rocas y escondites (la prueba de aceptación); se retoman si sobra tiempo en P9.
