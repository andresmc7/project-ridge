# THE HOLLOW — Documento de diseño del nivel (vertical slice, v3)

Objetivo: un nivel terminado, presentable a cualquiera, que se sienta como el primer capítulo de un juego completo.
Este documento manda sobre los layouts anteriores. Lo que no esté aquí, lo decide `docs/MASTER_SPEC.md`.
Implementación: `HollowLayout.cs` (datos del diseño) + `HollowBuilder`/`HollowProps`/`HollowAtmosphere` (generación).

---

## 1. Concepto en una frase

Un padre llega de noche a la estación de guardabosques donde su familia lo esperaba. No están. Algo alto y
delgado se mueve entre los árboles, y luces rojas caen del cielo barriendo el bosque en busca de gente. Tiene
que seguir el rastro de su familia hasta la radio de la camioneta sin que lo vean.

## 2. Pilares (todo se evalúa contra esto)

1. **Esconderse, observar, avanzar.** El loop es siempre el mismo y siempre claro (sección 3). Pelear es una
   salida de emergencia, nunca el camino.
2. **La luz es la amenaza.** Los reflectores rojos te llevan; tu linterna te ayuda a ver pero te delata. La
   oscuridad y el pasto alto protegen.
3. **Lo que ves es lo que hay.** El círculo rojo es exactamente la zona que te atrapa; el cono en el suelo es
   exactamente lo que ve el Lurker. Los patrones son exactos y se pueden aprender. Nada mata sin aviso.
4. **Esperanza.** Cada pista cuenta algo pequeño y concreto de la familia. Ninguna resuelve el misterio.

## 3. El loop (núcleo del juego)

```
  ESCONDERSE  →  OBSERVAR  →  AVANZAR  →  (siguiente escondite)  →  ... → CHECKPOINT → siguiente sala
  agachado en      el patrón completo     cuando la ventana
  pasto dorado     se ve desde ahí        está abierta
```

Reglas (claras y sin excepciones):

| Amenaza | Te detecta si… | Estás a salvo si… | Si falla |
|---|---|---|---|
| **Reflector rojo** (círculo en el suelo) | estás dentro del círculo sin esconderte ~0.65 s (el borde brillante es aviso, no castigo) | agachado en el pasto alto, o fuera del círculo | la nave te lleva → último checkpoint |
| **Lurker** (cono rojo en el suelo) | estás dentro del cono con línea de vista; se enciende naranja al sospechar | agachado en el pasto alto, o detrás de una roca agachado, o fuera del cono | te persigue; si te alcanza, 3 golpes te matan |

- **Pasto alto dorado = escondite.** Se reconoce de un vistazo (color, altura, contorno redondo). El HUD dice
  `HIDDEN — THEY CAN'T SEE YOU`.
- **Patrones exactos**: cada reflector recorre una ruta fija (lazo o ida y vuelta) con pausas; su posición
  depende solo del reloj. Cada Lurker patrulla un lazo corto con pausas mirando hacia afuera.
- **Sonido como aviso**: cada reflector zumba sobre su círculo (se oye venir); alarma al empezar a verte.
- **Herramientas**: piedra (G/LB) para atraer a un Lurker a otro lado; bengala para aturdir a quemarropa.

## 4. Estilo: Echoes of Wisdom × Alan Wake

| Referencia | Qué tomamos | Implementación |
|---|---|---|
| Zelda: Echoes of Wisdom | **Diorama**: cámara alta mirando al norte, salas legibles del tamaño de la pantalla, formas redondeadas, objetos de juego con lectura inmediata, acentos de color (hongos luminosos) | Cámara yaw 0 / pitch 55; salas con cornisas; regla de oclusión; mallas chunky; hongos emisivos en los bordes |
| Alan Wake 2 | Oscuridad densa, niebla que se arrastra desde el bosque, la luz como amenaza y herramienta, grano | Niebla solo fuera del escenario; haces volumétricos rojos; linterna con cookie; grano y viñeta |
| The Last of Us Parte II | Textura y desgaste: madera húmeda, barro, charcos, objetos que cuentan historia | Texturas Poly Haven, charcos con reflejo de cielo nocturno, props del campamento y la camioneta |

**Regla de oclusión (sin transparencias)**: la cámara mira al norte con 55° de inclinación. Nada al sur del
suelo caminable puede medir más de `1.43 × s − margen` (s = metros hasta lo caminable hacia el norte). Por
eso los bordes del lado de la cámara son cornisas bajas y arbustos, y los acantilados altos con bosque quedan
al norte (fondo). Ningún árbol tapa nunca al papá; no hace falta desvanecer nada.

## 5. Verbos del jugador

| Verbo | Control | Para qué sirve |
|---|---|---|
| Moverse | stick / WASD | giro casi instantáneo (media vuelta 0.1 s), avance siempre hacia donde mira |
| Agacharse | C / B | **esconderse en el pasto alto**, perfil bajo detrás de rocas |
| Lanzar piedra | G / LB | distraer: el impacto atrae al Lurker a ese punto |
| Usar | E / A | recoger, registrar, pistas, la radio |
| Garrote | clic / RT | golpe por sorpresa por la espalda; defensa |
| Bengala | Q / RB | a quemarropa aturde; luz y ruido |
| Botiquín | H / ↑ | +vida |

## 6. Estructura: refugio + cinco salas

Ruta de oeste a este (~130 m). Cada sala cabe en pantalla: desde el escondite de entrada se ve el patrón entero.

| # | Sala | Amenazas | Qué enseña |
|---|---|---|---|
| 0 | **Estación 7** (refugio sin techo, pared sur en ruinas) | ninguna | moverse, usar, la foto de la familia; pastizal de práctica junto a la salida |
| 1 | **Primera luz** | 1 reflector en lazo rectangular lento | el reflector pasa por encima del pasto y no te ve; el centro es una isla segura |
| 2 | **El arroyo** | 1 Lurker en lazo en la otra orilla; puente único | leer el cono, cruzar cuando mira a otro lado; piedras antes de la sala. Aparición con guion al entrar a la sala 1 (un Lurker en el acantilado norte se detiene a mirarte) |
| 3 | **Dos luces** | 2 reflectores: uno barre el camino de punta a punta, otro lo cruza por el centro | islas en zigzag; la bengala está justo en el cruce (riesgo/recompensa) |
| 4 | **La espesura** | 1 Lurker en lazo alrededor de unas rocas + 1 reflector que guarda la salida | combinar: escondite dentro del lazo, rocas como cobertura, cronometrar dos patrones |
| 5 | **El campamento y la camioneta** | 2 Lurkers (campamento y camino) + 1 reflector en lazo alrededor de la camioneta | todo junto; la radio (clímax) |
| — | **Clímax** | la radio acelera los reflectores cercanos y enciende 2 más; el ruido atrae a los Lurkers | aguantar 25 s escondido en el pasto junto a la camioneta |

Checkpoints en los pasillos entre salas (fuera de rutas y conos) y junto al campamento antes de la radio. Al
reaparecer, 6 s de gracia para Lurkers y reflectores, y los Lurkers vuelven al punto de su ruta más lejano.

## 7. Escala

- Papá 1.8 m. Salas 18–22 × 12–14 m. Pasillos de 3.8 m.
- Reflector: círculo de 2.3 m de radio (detecta en el 85% interior), 2.2–3 m/s.
- Lurker: ve 9 m con 80° de cono; dentro del cono te detecta en ~0.3–1 s según la distancia.
- Pastizales escondite: radio 1.3–1.9 m, separados ≤ 5 m dentro de cada sala.

## 8. Enemigos

**El Lurker**: 2.5 m, delgado, brazos largos, ojos rojos, tics bruscos. Patrol → Investigate → Search →
Chase (zig-zag) → Attack (telegrafiado 0.45 s) → Search. Oye pasos (trotar se oye a 7 m, caminar a 3.5 m).

**Los reflectores**: nunca se ve la nave, solo sus haces rojos y su zumbido.

## 9. Animación

Personajes humanoides con clips reales (Quaternius, CC0): idle, walk, jog, sprint, crouch, golpe, disparo,
lanzar, recoger, usar, golpe recibido, muerte, levantarse. Capa procedural encima: mirada, tics del Lurker.

## 10. Audio

Cada evento suena. Música calma / persecución, latido, zumbido 3D por reflector, alarma al ser visto, pasos por
superficie, arroyo, lluvia, la radio.

## 11. UI

Mínima: estado de sigilo (SAFE / HIDDEN / ALERT / HUNTED) con barra de sospecha o exposición, objetivo que
enseña el loop en el primer checkpoint, inventario breve. Táctil: joystick + botones contextuales.

## 12. Criterios de "terminado"

- Se juega de principio a fin sin ayuda (verificado con el bot de playtest `Project Ridge/QA/Run Loop Bot`).
- Reglas del loop verificadas en play: escondido bajo la luz no te lleva; de pie en el cono te detectan;
  escondido junto al Lurker no sospecha; las salas no se pueden abandonar por los bordes.
- Ningún objeto tapa el suelo caminable (regla de oclusión en el generador).
- 30 fps estables en iPhone 16 (A18), verificado con Profiler en dispositivo (pendiente: no hay dispositivo).
