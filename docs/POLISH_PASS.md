# POLISH PASS — The Hollow a calidad de producto terminado

Autor: Claude (hub de diseño), 2026-10-20. Basado en una sesión jugada por Andres (capturas), revisión del código y referencias del género.
Este documento **manda** sobre `THE_HOLLOW_DESIGN.md` y `MASTER_SPEC.md` donde choquen. Imagen objetivo: `docs/ref/target_look.png`.

---

## 0. Cómo ejecutar este documento (léelo primero)

- **Modo autónomo.** No preguntes. Cada decisión de diseño ya está tomada aquí. Si algo es ambiguo, elige lo que mejor sirva a los pilares (sección 2), anótalo en `docs/DECISIONS.md` (fecha, decisión, por qué) y sigue.
- **Orden:** fases P0 → P9 (sección 5). Cada fase termina con: compila sin errores, play mode sin errores, su prueba de aceptación ejecutada, capturas antes/después en `docs/progress/<fase>_before.png` y `_after.png` (usa `Shots.Capture` con el encuadre de juego), entrada en `BUILD_STATUS.md` y commit con push.
- **Bloqueos:** si una fase depende de algo que no hay (por ejemplo los archivos de Mixamo en `_incoming/mixamo/`), haz todo lo demás de esa fase y pasa a la siguiente. Vuelve cuando aparezcan los archivos. Nunca te detengas a esperar.
- **Comparación visual:** al final de P3, P6 y P9, pon lado a lado una captura de juego y `docs/ref/target_look.png` (un PNG compuesto en `docs/progress/compare_<fase>.png`) y escribe en `BUILD_STATUS.md` 5 diferencias concretas que quedan. Ataca las 3 mayores antes de cerrar la fase.
- **Prohibido:** herramientas `Unity_AssetGeneration_*`, servicios de pago, assets sin licencia clara. Todo asset nuevo va a `ASSETS.md`.
- **Generadores:** el nivel se regenera con `Project Ridge/The Hollow/Build All`. Los cambios de contenido van en los generadores (no a mano en la escena), para que Build All siga siendo la fuente de verdad.

---

## 1. Diagnóstico (lo que está mal hoy)

| # | Problema | Evidencia | Causa raíz |
|---|---|---|---|
| D1 | Correr se ve frenético y feo | Capturas; `CharacterBuilder.FatherController` | Los clips se reproducen al 1.26–1.32× para alcanzar 3.2 y 4.2 m/s (natural: trote 2.46, sprint 3.19). Patinan y aceleran el ciclo. |
| D2 | Giros robóticos | `PlayerMovementConfig.turnSpeed = 1800`°/s | Media vuelta en 0.1 s, sin pivote ni inclinación. |
| D3 | Recoger se ve mal | `PickUp_Table` | Animación de recoger de una mesa, usada para objetos en el suelo. |
| D4 | El papá corre apuntando un arma | `FatherAnimator.Update`: garrote siempre activo si `HasClub` | No existe enfundar ni desenfundar. Además el golpe usa `Sword_Regular_A`. |
| D5 | Demasiados botones táctiles | `HudBuilder`: ATTACK, USE, CROUCH, SPRINT, HEAL, FLARE, THROW, pausa | Un botón por verbo. Imposible de usar con dos pulgares en un iPhone. |
| D6 | La captura del OVNI no tiene peso | `Searchlight.Detect` → `GameFlow.OnCaptured` → fundido | Ni la luz ni el jugador se "bloquean". No hay parálisis ni temblor. |
| D7 | El pasto escondite son círculos regados | Capturas; pastizales de radio 1.3–1.9 m | Colocación por puntos con contorno redondo, no por masas naturales. |
| D8 | Árboles como erizos azules | Capturas (canopia azul de púas) | Coníferas "chunky" vistas desde arriba: solo se ve la copa. Saturación azul. |
| D9 | Rocas como bolas lisas | Capturas | Rocas procedurales lisas, sin material ni silueta. |
| D10 | El personaje se ve diminuto | Cámara a 18.5 m, FOV 38 | En un iPhone el papá mide ~4% del alto de la pantalla. En Echoes of Wisdom, ~10–12%. |
| D11 | Personaje genérico | Cuerpo `Superhero_Male` de Quaternius con ropa procedural | Proporciones de superhéroe, no de un papá. |
| D12 | El cono del Lurker pintado en el suelo | `VisionCone` | Lectura de juego de estrategia (Commandos), no de thriller. Rompe la inmersión. |
| D13 | HUD con mucho texto | Esquina superior izquierda con 5 líneas | Contadores siempre visibles y objetivos largos. |
| D14 | Botones de fin en la pantalla de título | Captura: "PLAY AGAIN / QUIT" bajo el título | `endButtons` activos en el estado Title. |
| D15 | Sin música de suspenso real | Música sintetizada | Falta una partitura por capas (calma, tensión, persecución) con stingers. |

## 2. Pilares y lo que NO se toca

**Se queda (a Andres le gusta):** los haces de luz del OVNI (volumétricos), el bokeh/tilt-shift, el sonido ambiente (lluvia, arroyo, pisadas, voces del Lurker). Se pulen, no se reemplazan.

**Pilares (todo se evalúa contra esto):**
1. **El cuerpo cuenta la historia.** Un papá común, asustado y con peso. Cada animación tiene que poder salir en un tráiler.
2. **Un pulgar mueve, el otro decide.** En táctil: joystick + 1 botón contextual + 1 botón de objeto. Lo demás es automático o contextual.
3. **La oscuridad protege, la luz caza.** Contraste fuerte. Lo legible sale de la luz, no de marcas pintadas.
4. **Sobrevivir, no matar.** Los recursos aturden y distraen. Nada se resuelve a golpes.
5. **Diorama vivo.** Composición de Echoes of Wisdom (sala legible, cámara de 3/4) con materiales y luz de The Last of Us / Alan Wake.

---

## 3. Decisiones de diseño

### 3.1 Controles (táctil primero)

Referencias: INSIDE en iOS se juega con un dedo, sin botones (deslizar, tocar y mantener), y la crítica lo destaca. Los juegos de sigilo modernos (Horizon, Ghost of Tsushima, A Plague Tale) esconden al jugador en vegetación densa sin pedir combinaciones. Los shooters móviles fijan el sprint empujando el joystick hacia adelante en lugar de usar un botón.

**Pantalla horizontal:**
- **Mitad izquierda: joystick flotante.** Aparece donde apoyas el pulgar, en cualquier punto del 45% izquierdo de la pantalla. Se ve como un anillo tenue (sin disco gris).
  - Inclinación < 0.35 → **caminar sigiloso** (lento y silencioso).
  - 0.35 – 0.85 → **caminar/trotar** (blend continuo).
  - Llevarlo al borde y mantenerlo 0.25 s, o arrastrar más allá del anillo → **sprint fijado** (el anillo se ilumina). Se cancela al soltar o al frenar.
- **Mitad derecha: dos botones, nada más.**
  1. **ACCIÓN** (grande, 110 pt, abajo a la derecha). El ícono cambia según el contexto, por prioridad: Inspeccionar pista › Usar (radio) › Recoger › Registrar › Derribo por la espalda › Golpe (si hay Lurker a ≤ 2.2 m y tienes garrote) › Empujar/esquivar (sin garrote). Si no hay contexto, **se oculta** (alfa 0.15).
  2. **OBJETO** (mediano, 84 pt, encima y a la izquierda de ACCIÓN). Muestra el objeto activo (piedra o bengala) con su cantidad. **Tocar** = lanzar al objetivo inteligente (punto de interés o Lurker más relevante delante, máx. 12 m). **Mantener y arrastrar** = apuntar con un arco visible; soltar = lanzar; arrastrar de vuelta al botón = cancelar. **Deslizar arriba/abajo sobre el botón** = cambiar de objeto.
- **Iconos pequeños en la esquina superior derecha:** linterna (on/off) y pausa.
- **Curarse:** tocar el indicador de vida (arriba a la izquierda) y **mantener 0.6 s** usa un botiquín. Así se evita usarlo sin querer.
- **No hay botones de agacharse ni de sprint** (ver 3.2).
- **Teclado y control** (para pruebas): WASD; Shift = sprint; C = forzar agachado (solo pruebas, oculto); E = ACCIÓN; Q = OBJETO; Tab = cambiar objeto; F = linterna; H = curar; Esc = pausa. En el control: A = ACCIÓN, RB = OBJETO, LB = cambiar objeto, Y = linterna, cruceta arriba = curar.
- **Hápticos (iOS):** pulso leve con cada paso cercano del Lurker a ≤ 6 m, fuerte al ser detectado, zumbido continuo durante la captura. Si requiere un plugin nativo, haz uno mínimo de Core Haptics (`UIImpactFeedbackGenerator`) en `Assets/Plugins/iOS/`. Marca `UNVERIFIED` hasta probarlo en un dispositivo.

### 3.2 Sigilo automático

**Agacharse es automático y siempre lógico.** El jugador nunca piensa en "agacharse"; piensa en "esconderse" y "no hacer ruido".

| Condición | Postura |
|---|---|
| Dentro de vegetación densa (densidad de escondite ≥ 0.5 en la posición) | **Agachado oculto** (transición de 0.25 s) |
| A ≤ 1.0 m de cobertura baja (roca, tronco, muro < 1.4 m) y hay una amenaza a ≤ 18 m (Lurker no dormido o haz a ≤ 12 m) | **Agachado tras cobertura** (de espaldas a la cobertura si estás quieto) |
| Joystick < 0.35 y hay una amenaza a ≤ 18 m | **Caminar sigiloso agachado** |
| Trotar o correr | De pie, siempre |
| Sin amenazas cerca y fuera de la vegetación | De pie |

- Histéresis: para levantarse hacen falta 0.4 s seguidos fuera de las condiciones. Así no parpadea.
- El HUD no dice "HIDDEN" con texto. Cuando estás oculto, la pantalla se oscurece levemente en los bordes, el sonido se amortigua un poco (low-pass suave) y el personaje se ve en silueta. Eso es todo.
- **Ruido:** sigiloso 1 m, caminar 3.5 m, trotar 7 m, correr 12 m. Ramas secas en el suelo: +4 m, marcadas visualmente (claras, quebradizas).

### 3.3 Armas y recursos (supervivencia, no shooter)

- **Por defecto, el papá va con las manos libres** y la linterna en la izquierda.
- **Garrote:** va **enfundado en la mochila** (visible en la espalda). Se desenfunda solo cuando un Lurker está en Chase a ≤ 6 m o al pulsar ACCIÓN en contexto de golpe, y se enfunda 3 s después de que no hay amenaza. Animación de desenfundar y enfundar (0.35 s, capa de brazo).
  - Golpe = **aturde 2.5 s y empuja**: abre una ventana para escapar. No mata. Durabilidad 4; al romperse hace mucho ruido.
  - **Derribo por la espalda** (agachado, sin ser detectado): incapacita al Lurker **20 s** (cae y convulsiona). Luego se levanta en Search. Gasta 1 de durabilidad.
- **Pistola de bengalas:** enfundada en la cadera. Solo se ve al apuntar o disparar. Disparada lejos cae y arde 8 s con luz y ruido (radio 18 m): todos los Lurkers que la oyen la investigan. A quemarropa: aturde 4 s. Máximo 2 en el inventario; hay 3 en todo el nivel.
- **Piedras:** máximo 3. Se recogen en puntos concretos (pilas de piedras junto a caminos). Distracción: ruido de 9 m donde caen.
- **Botiquín:** máximo 2. Hay 3 en el nivel.
- **Se elimina la chatarra y la fabricación.** No aportan al PoC y complican el HUD.
- **Ningún enemigo muere en el PoC.** Siempre vuelven a levantarse. Eso mantiene el horror.

### 3.4 Captura del OVNI (la secuencia que pidió Andres)

Fases, con tiempos:

1. **Exposición** (0 → 1, como hoy: ~0.65 s dentro del círculo sin esconderse). Mientras sube: el zumbido sube de tono, el borde del círculo parpadea y aparece una viñeta fría.
2. **BLOQUEO (0 s):** el haz **se detiene en seco**, se estrecha un 30% sobre el jugador y su color pasa de rojo a blanco frío. **El jugador también se detiene en seco**: el input se ignora y el CharacterController se apaga.
3. **Parálisis (0 – 1.6 s):** el cuerpo queda rígido en una pose de shock (brazos un poco separados y dedos abiertos; usa un clip "Electrocuted/Shocked" de Mixamo o, mientras no exista, una pose procedural). Encima, **temblor de alta frecuencia** en columna, brazos y cabeza (ruido de 18–25 Hz, amplitud de 1.5° que sube a 4°), como una corriente eléctrica. La linterna parpadea. Pantalla: aberración cromática que sube, grano, la saturación baja a 0.3 y la cámara se acerca un 12% en 1.6 s. Audio: crepitar eléctrico + zumbido grave + los latidos se cortan. Háptico continuo.
4. **Forcejeo (1.6 – 3.0 s), una vez por vida:** aparece un anillo alrededor del papá. **Toques rápidos en cualquier parte de la pantalla** (o mover el joystick de lado a lado) lo llenan; hacen falta ~10 toques en 1.4 s. Si lo llenas: el papá se suelta con un tirón, cae rodando, el haz "retrocede" y queda ciego 2.5 s. Huye.
5. **Elevación (si no se suelta):** los pies se despegan, sube 3 m en 1.5 s con el temblor al máximo, destello blanco, negro y último checkpoint.
- El checkpoint y el respawn se mantienen como están (6 s de gracia).

### 3.5 Legibilidad de la IA (sin conos pintados)

- **Se elimina `VisionCone` del juego normal.** Queda como opción de accesibilidad ("Ayudas de sigilo") en el menú de pausa, apagada por defecto.
- La información sale del propio Lurker:
  - **Ojos:** brasa tenue en Patrol, naranja intenso y parpadeante en Investigate/Search, rojo con un leve haz en Chase.
  - **Cabeza:** siempre gira y apunta hacia donde mira (IK de cabeza). Así el jugador lee el cono por la postura.
  - **Indicador de detección** (estilo TLOU): un arco pequeño sobre la cabeza del Lurker que se llena de blanco a naranja mientras te va detectando. Solo aparece si la sospecha es > 0.05. Si el Lurker está fuera de la pantalla, aparece una flecha en el borde.
  - **Sonido:** chasquido gutural al sospechar, chillido corto al detectar y respiración audible a ≤ 8 m.

### 3.6 Cámara

- Pitch **42°** (hoy 50°), distancia **12.5 m** (hoy 18.5 m), FOV **35°**. Objetivo: el papá ocupa **10–12% del alto de la pantalla**. Mídelo en la captura y ajusta hasta lograrlo.
- **Mirar hacia adelante:** desplazamiento de 1.5 m en la dirección de avance (con suavizado de 0.4 s), para ver lo que viene.
- **Amenaza:** si hay un Lurker en Chase o un haz a ≤ 10 m, la cámara se aleja hasta 14.5 m y se encuadra entre el jugador y la amenaza (peso 0.3).
- Se mantienen el tilt-shift y el bokeh, con el plano focal en el jugador. El desenfoque de los bordes superior e inferior debe seguir siendo sutil.
- Con la cámara más baja vuelven a importar los objetos en primer plano: activa el desvanecimiento por oclusión con dithering (no transparencia) para todo lo que tape al jugador.

### 3.7 Animación (nivel producto terminado)

**Reglas:**
- **Nunca** escales el tiempo de un clip de locomoción por encima de 1.1× ni por debajo de 0.85×. Las velocidades de movimiento **salen de los clips**: mide la velocidad natural de cada clip (desplazamiento de la raíz del clip con root motion / duración) y pon esa velocidad en `PlayerMovementConfig`.
- **Locomoción con blend tree 2D** (velocidad × giro): idle, caminar sigiloso, caminar, trotar y correr, con variantes de giro izquierda/derecha si hay clips.
- **Arranques y frenadas:** clips de start y stop (o, si no existen, inclinación + 2 pasos de transición). Con frenada brusca: un clip de "frenar" o una inclinación de 8° hacia atrás.
- **Giros:** velocidad de giro de 540°/s en movimiento. **Pivote** con un clip de giro de 180° corriendo o, como alternativa, giro en el sitio de 0.25 s. Parado: giro en el sitio con los pies (clips de turn 90/180) si el ángulo es > 70°.
- **Inclinación en curvas:** columna inclinada según la velocidad angular (máx. 7°), suavizada.
- **IK de pies** (Mecanim foot IK + raycast al suelo) en pendientes, escalones y el puente. El cuerpo baja la cadera para que ningún pie flote.
- **IK de mano** (Animation Rigging, paquete gratis de Unity): la mano izquierda sostiene la linterna apuntando hacia donde mira el cuerpo. La mano del garrote, al desenfundar.
- **Mirada:** la cabeza mira al interactuable cercano o a la amenaza (como hoy) y, en idle largo, mira alrededor nervioso.
- **Idle con vida:** respiración, cambios de peso y, después de 6 s quieto, un "mirar alrededor asustado". Si hay una amenaza cerca, idle tenso (encorvado y respirando rápido).
- **Recoger:** del suelo, con rodilla (clip correcto). La mano llega al objeto por IK y el objeto se pega a la mano en el frame de contacto.
- **Registrar, inspeccionar, usar la radio:** cada uno con su clip y con el personaje alineado y orientado al objeto antes de empezar (0.2 s de alineación).
- **Golpe con garrote:** swing de bate de una o dos manos, no de espada. Con anticipación, impacto y recuperación. Hit-stop de 60 ms en el impacto.
- **Recibir golpe, tambalearse, caer, morir y levantarse:** clips dedicados.
- **Lurker:** locomoción de criatura (clips "mutant" o "creature"), con **quietud total** (pose congelada de 0.6–2 s) y **arranques en zig-zag** violentos durante la persecución. Encima, una capa procedural de tics (cabeza y dedos) como ya existe. Ataque telegrafiado (anticipación de 0.45 s). Convulsión al estar aturdido.

**Fuente de clips:**
- **Mixamo** (gratis, uso comercial sin regalías). Requiere una cuenta de Adobe, así que la descarga la hace Andres (o Claude con él, desde su navegador). La lista está en el anexo A. Los archivos llegan a `_incoming/mixamo/`. Mientras no estén, usa los mejores clips de Quaternius UAL1/UAL2 **sin escalar el tiempo** (velocidades = las naturales del clip) y arregla D2, D3 y D4 igual.
- Importa los clips como Humanoid, con root motion solo para medir velocidades, e "In Place" para locomoción.

### 3.8 Personajes

- **El papá:** cambia `Superhero_Male_FullBody` por un cuerpo de proporciones normales (1.80 m, complexión media, 38 años).
  - **Ruta A (preferida, autónoma):** Blender (gratis; instálalo con `winget install BlenderFoundation.Blender`) + **MPFB2** (extensión oficial de MakeHuman para Blender, con assets CC0). Genera el personaje con un script de Blender sin interfaz (`blender -b -P`): varón de 38 años, complexión media, pelo corto, barba de pocos días. Ropa: chaqueta de trabajo, jeans y botas, de los assets CC0 o CC-BY de MakeHuman (anota cada licencia). Proxy de ojos y cuerpo de baja resolución. Rig "Game engine". Exporta FBX a `Assets/ThirdParty/MakeHuman/`. Objetivo: ≤ 15k triángulos y texturas de 1k.
  - **Ruta B (si la A falla después de 2 h de intento):** el cuerpo masculino regular de Quaternius Universal Base Characters (no el Superhero), con ropa mejor modelada. Anota el fallo de la ruta A en `DECISIONS.md`.
  - **Material:** piel con SSS falso (wrap lighting en el shader URP), ropa con normal maps de tela (Poly Haven), barro y humedad en botas y jeans (máscara de altura), mochila visible con el garrote enfundado.
- **El Lurker:** mantén la base deformada, pero con material de piel húmeda oscura (roughness bajo en manchas, sheen frío), extremidades 15% más largas, cuello más largo, sin ropa y ojos emisivos pequeños. Silueta asimétrica: un hombro más alto y un brazo más largo.

### 3.9 Entorno y nivel

- **Árboles:** reemplaza las coníferas "chunky" de púas por **coníferas con tronco visible y ramas en tarjetas** (atlas `twig` de `fir_tree_01`, que ya está en el proyecto). Copas en verde oscuro casi negro, con contraluz de luna en los bordes. **Cero azul saturado.** Con la cámara a 42° tienen que verse troncos. Tres o cuatro variantes de altura y forma, algunos árboles muertos y tocones. LOD con impostor (billboard) a > 30 m.
- **Rocas:** usa los sets `rock_moss_set_01` y `_02` y `boulder_01` de Poly Haven (ya importados), decimados a ≤ 3k triángulos por roca para móvil, con musgo en la cara superior (shader por normal). Nada de esferas lisas.
- **Vegetación escondite natural:** la colocación sale de un **mapa de densidad** (ruido fBm + máscara por rasgos del terreno: más denso junto a rocas, bordes de caminos, zonas bajas y húmedas, y orillas del arroyo).
  - Masas irregulares de 0.6 m a 8 m, con bordes que se difuminan y especies mezcladas (pasto alto, helechos, arbustos bajos).
  - **Nunca círculos.** Valida con una captura cenital: ningún contorno debe parecer un círculo.
  - **El escondite se calcula por densidad en la posición del jugador** (muestreo del mapa), no con triggers circulares.
  - Legibilidad: las puntas del pasto escondite captan algo más de luz (cálida y sutil). Sin dorado chillón.
- **Suelo:** ver mezcla de capas (barro, hojarasca, roca, musgo), charcos con reflejo, ramas secas que hacen ruido (marcadas).
- **Composición por sala:** cada sala tiene **un hito visual** que se ve desde la entrada (refugio con farol; puente; árbol muerto gigante; restos humeantes de algo que cayó; camioneta con el dial ámbar). **Las luces cálidas marcan el camino; las frías, el peligro.**
- **Bordes de sala:** cornisas y vegetación densa, no muros geométricos.

### 3.10 Luz y post-proceso

- **Más oscuro y con más contraste.** Hoy todo tiene una niebla azul uniforme. Objetivo: el 60% de la pantalla en sombra profunda, con "islas" de luz.
- **Luna:** luz principal fría, a contraluz (por detrás de la escena respecto a la cámara), para recortar siluetas. Intensidad baja.
- **Ambiente:** baja la luz ambiental azul un 40%. Mantén probes de luz para personajes.
- **Linterna:** clave cálida (3200 K), con cookie, sombras, alcance de 14 m y un halo suave (sin disco blanco).
- **Haces del OVNI:** se quedan. Ajustes: el anillo del suelo pasa de neón grueso a un borde fino que parpadea, con caída suave hacia adentro; partículas de polvo dentro del haz; el haz ilumina de verdad lo que toca (luz spot con cookie).
- **Niebla:** por altura (se arrastra por el suelo en zonas bajas), no un velo uniforme.
- **Post:** ACES, contraste +15, tinte de sombras verde azulado oscuro, luces altas cálidas, grano fino, viñeta. **Bokeh/tilt-shift: se queda.**
- Verifica con un histograma de la captura: ≥ 50% de los píxeles con luminancia < 0.08 y ≥ 3% por encima de 0.6.

### 3.11 Audio y música

- **Música por capas** (crossfade de 1.5 s entre estados) con el `AudioDirector`:
  - **Explorar:** dron grave + texturas espaciadas (piano preparado o cuerdas lejanas).
  - **Tensión** (sospecha > 0.3, Lurker a ≤ 12 m o haz a ≤ 10 m): pulso grave de cuerdas + latido.
  - **Persecución:** percusión y cuerdas agresivas.
  - **Stingers:** encontrar una pista, ser detectado, la captura, el checkpoint, el final.
- **Fuentes:** música CC0 o de dominio público, descargable sin cuenta: FreePD (https://freepd.com, categorías Horror y Mystery) y OpenGameArt filtrando por CC0. Anota cada pista en `ASSETS.md`. Si no hay piezas adecuadas, compón las capas con `SoundSynth`, pero con instrumentos reales sampleados CC0 (por ejemplo, samples de cuerdas o piano CC0 de Freesound), no con ondas puras.
- Mezcla: mide el loudness integrado de cada capa y normaliza (música −20 LUFS, efectos −16, ambiente −24).

### 3.12 HUD

- **Fuera:** contadores de texto permanentes, "SAFE/HIDDEN/ALERT/HUNTED" en grande y objetivos de varias líneas.
- **Queda:**
  - Vida como un pequeño ícono de corazón o latido arriba a la izquierda (solo se ve si la vida es < 100% o al tocarlo) + viñeta roja y cojera con poca vida.
  - Botón OBJETO con su ícono y cantidad.
  - Indicador de detección sobre los Lurkers (3.5).
  - Objetivo: **una frase corta** que aparece 4 s al entrar a una sala nueva y se desvanece. Se vuelve a ver al pausar.
  - Aviso al recoger: ícono + "+1" junto al botón OBJETO, 1.2 s.
- **Tipografía:** una sola sans condensada con buen peso (Google Fonts, licencia OFL; anótala en `ASSETS.md`). Tamaño mínimo 14 pt en un iPhone de 6.1".
- **Arreglar D14:** los botones de fin no aparecen en la pantalla de título.

---

## 4. Lista de verificación de "producto terminado" (aplica a todas las fases)

- [ ] 60 s de juego sin ninguna animación que patine, flote, se corte o se vea "de robot" (revisa a velocidad 0.25× en el editor).
- [ ] Ningún objeto en la mano sin motivo.
- [ ] Toda acción tiene anticipación, contacto y recuperación, y suena.
- [ ] En táctil se puede terminar el nivel con dos pulgares y sin mirar los botones.
- [ ] La captura comparada con `target_look.png` se ve del mismo "mundo" (paleta, contraste, escala del personaje).
- [ ] 0 errores y 0 advertencias propias en la consola.

---

## 5. Fases de ejecución

| Fase | Contenido | Aceptación (ejecutada en play) |
|---|---|---|
| **P0** | Checkpoint git. Arreglos rápidos: D14, garrote enfundado (D4), recoger del suelo (D3), turnSpeed 540 (D2), clips de locomoción sin escalar (D1, con velocidades medidas), quitar chatarra y fabricación | Captura corriendo y recogiendo; el garrote no está en la mano al caminar; ninguna velocidad de clip fuera de 0.85–1.1× |
| **P1** | Controles táctiles nuevos (3.1) + sigilo automático (3.2) + teclado y control | Recorrer las salas 1–3 solo con joystick + ACCIÓN + OBJETO (pulsaciones virtuales); se agacha solo en la vegetación y tras cobertura; sprint por empuje al borde; 0 botones de agacharse/sprint |
| **P2** | Captura del OVNI (3.4) | Secuencia completa grabada en 8 capturas (bloqueo, parálisis, forcejeo con éxito, forcejeo fallido, elevación); el haz no se mueve durante el bloqueo |
| **P3** | Cámara (3.6) + luz y post (3.10) | El papá ocupa 10–12% del alto; histograma dentro del objetivo; comparación con la referencia |
| **P4** | Entorno: árboles, rocas, vegetación natural por densidad (3.9) | Captura cenital sin círculos; troncos visibles desde la cámara de juego; escondite por densidad probado (entrar a una masa → oculto; borde fino → no) |
| **P5** | Legibilidad de la IA (3.5) | Sin cono en el suelo; ojos e indicador cambian con el estado; la flecha en el borde aparece con el Lurker fuera de cámara |
| **P6** | Personajes nuevos (3.8) | Papá nuevo en juego con las mismas animaciones; ≤ 15k triángulos; comparación con la referencia |
| **P7** | Animación completa (3.7), con Mixamo si ya están los archivos | Lista de verificación de la sección 4 a 0.25×; starts, stops, pivote, giro en el sitio, IK de pies en el puente |
| **P8** | Música y audio (3.11) + HUD (3.12) + hápticos | Cambios de capa medidos (estados → capa activa); loudness dentro del objetivo; HUD con ≤ 4 elementos visibles en exploración |
| **P9** | Pase final: recorrido completo con el bot + revisión con ojos frescos | Nivel completo sin ayuda; lista de la sección 4 completa; `BUILD_STATUS.md` con lo `UNVERIFIED` honesto; build de Windows actualizado |

---

## Anexo A — Clips de Mixamo (para Andres o para Claude con su navegador)

Ajustes de descarga: **FBX for Unity**, **Without Skin**, 30 fps; **In Place** activado en toda la locomoción. Guárdalos en `C:\Dev\ProjectRidge\_incoming\mixamo\` con el nombre del clip.

**Papá** (buscar por estas palabras):
- Idle: "Breathing Idle", "Look Around" o "Nervously Look Around", "Scared/Terrified" (idle tenso)
- Locomoción: "Walking", "Sneak Walk" o "Crouched Walking", "Jog Forward", "Running", "Fast Run"
- Agachado: "Crouch Idle", "Crouched Walking", "Standing To Crouched", "Crouched To Standing"
- Arranques, frenadas y giros: "Start Walking", "Stop Walking", "Running Stop", "Left Turn 90", "Right Turn 90", "Standing Turn 180", "Running Turn 180"
- Acciones: "Picking Up" (del suelo), "Kneeling Pointing" o "Kneeling Examine", "Opening" (registrar), "Pressing Button" (radio), "Throw Object", "Baseball Swing" o "Melee Swing", "Pistol Aim" + "Pistol Shoot" (bengala)
- Daño: "Hit Reaction", "Stumble Backwards", "Dying", "Getting Up"
- Captura: "Electrocuted" o "Shocked"
- Enfundar: "Sheath Sword"/"Draw Sword" (se adaptan al garrote en la espalda)

**Lurker:**
- "Mutant Idle", "Mutant Walking", "Mutant Run", "Mutant Roaring", "Mutant Swiping", "Mutant Dying", "Zombie Scream", "Zombie Stand Up"
