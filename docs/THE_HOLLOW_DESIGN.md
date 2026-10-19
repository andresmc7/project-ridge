# THE HOLLOW — Documento de diseño del nivel (vertical slice)

Objetivo: un nivel terminado, presentable a cualquiera, que se sienta como el primer capítulo de un juego completo.
Este documento manda sobre el layout anterior (primer prototipo). Lo que no esté aquí, lo decide `docs/MASTER_SPEC.md`.

---

## 1. Concepto en una frase

Un padre llega de noche a la estación de guardabosques donde su familia lo esperaba. No están. Algo alto y
delgado se mueve entre los árboles, y una luz en el cielo barre el bosque buscando gente. Tiene que seguir el
rastro de su familia hasta la radio de la camioneta, sin pelear más de lo necesario.

## 2. Pilares (todo se evalúa contra esto)

1. **Vulnerable, no indefenso.** El papá puede pegar y tiene 2–3 bengalas, pero cada pelea cuesta vida,
   durabilidad y ruido. Esconderse siempre es la opción más segura.
2. **La luz es amenaza y salvación.** El reflector del OVNI te expone; tu linterna te ayuda a ver pero te
   delata; la bengala salva o condena (luz + ruido). La oscuridad protege, pero no deja ver.
3. **Leer el bosque.** El jugador anticipa por sonido (chasquidos, pasos, zumbido), silueta y el brillo de los
   ojos. Nada mata sin aviso: todo ataque tiene un telegrafiado claro.
4. **Esperanza.** Cada pista cuenta algo pequeño y concreto de la familia. Ninguna resuelve el misterio.

## 3. Referencias visuales y qué tomamos de cada una

| Referencia | Qué tomamos | Cómo se implementa en URP móvil |
|---|---|---|
| Zelda: Echoes of Wisdom / Link's Awakening | Lectura de **diorama**: cámara alta, formas redondeadas y biseladas, siluetas limpias, materiales con brillo "de juguete", desenfoque de miniatura (tilt-shift), mundo vivo con microanimación | Mallas chunky propias con bordes redondeados, DOF gaussiano por distancia, vegetación con viento por vértice, partículas ambientales (polillas, hojas, luciérnagas) |
| Alan Wake 2 | Bosque del noroeste del Pacífico de noche: **niebla densa** que se come el fondo, linterna como herramienta central, contraste fuerte, grano de película, cielo casi negro | Niebla exponencial + tarjetas de niebla baja, linterna con cookie y halo, grano, viñeta, aberración cromática sutil |
| The Last of Us Parte II | Textura y desgaste: madera húmeda, barro, objetos abandonados que cuentan historia, **lluvia reciente** (superficies mojadas, charcos) | Texturas fotográficas Poly Haven, materiales con más brillo en zonas mojadas, charcos (decals con reflejo), llovizna ligera con partículas |

**Gradación final (la "capa")**: sombras frías verde-azuladas, medios desaturados, luces prácticas cálidas
(farol, bengala) que resaltan contra el frío; luz del OVNI blanca-cian que lava el color. Contraste alto,
negros profundos sin aplastar la lectura del jugador (siempre con contorno/rim de luna).

## 3b. Ritmo: claustrofóbico y deliberado

- **Pequeño y denso, no grande y vacío.** Área jugable ~**70 × 45 m** (antes 130 × 80). Pasillos de bosque de
  3–5 m entre paredes de vegetación y roca; claros pequeños (10–14 m) donde la tensión sube.
- **Cámara más baja y cercana** (diorama apretado): se ve poco más allá de 12–15 m; la niebla y los troncos
  cierran el cuadro. Lo que no ves, lo oyes.
- **Velocidad de juego lenta**: caminar y agacharse son lo normal; correr es una decisión de pánico. Cada
  encuentro se resuelve **observando un patrón** (ruta del Lurker con pausas fijas, barrido del reflector con
  ciclo legible) y moviéndose en la ventana correcta.
- **Patrones aprendibles**: todas las patrullas son bucles cortos (20–30 s) con 2–3 pausas largas donde el
  Lurker mira en una dirección fija; el reflector recorre una ruta fija con una pausa larga en el centro. Sin
  aleatoriedad que castigue la observación (la aleatoriedad queda en los tics visuales y sonoros).
- **Cada paso tiene textura**: suelo con raíces, hojas, barro y charcos; nada de superficies lisas grandes.

## 4. Público, duración y dificultad

- Duración objetivo de la primera partida: **12–15 minutos** (lento, observando). Rejugada sabiendo la ruta: 5 min.
- Muerte: vuelve al último checkpoint, conservando lo recogido antes del checkpoint.
- Dificultad: un Lurker te mata en 3 golpes; huir de uno que te persigue es posible si rompes línea de vista
  (arbusto, pasto alto, rodear rocas) — el diseño de cada zona garantiza al menos una salida.

## 5. Verbos del jugador (mecánicas) y cómo encajan

| Verbo | Control | Para qué sirve | Riesgo |
|---|---|---|---|
| Caminar / trotar (stick análogo) / correr | stick, Shift / L3 | moverse | correr hace mucho ruido |
| Agacharse | C / B | sigilo, esconderse en pasto alto y arbustos, perfil bajo frente al reflector | lento |
| Linterna | F / Y | ver detalles, pistas y objetos | te hace muy visible a Lurkers y al OVNI |
| Usar | E / A | recoger, registrar, inspeccionar pistas, abrir, encender la radio | registrar hace ruido |
| Lanzar piedra (nuevo) | G / LB | **distraer**: el ruido atrae al Lurker al punto de impacto | piedras limitadas (se recogen en el suelo) |
| Garrote | clic / RT | golpe por sorpresa por la espalda (doble daño); defensa a quemarropa | durabilidad, ruido del impacto |
| Bengala | Q / RB | a quemarropa aturde; lanzada, luz + ruido que atraen lejos de ti | solo 2–3 en el nivel; el disparo te delata |
| Botiquín | H / ↑ | +50 vida | escaso, tarda 1 s (animación) |

Reglas de diseño:
- Toda zona enseña o combina verbos en este orden: **ver → esconderse → distraer → pelear (opcional)**.
- El jugador nunca está obligado a pelear. El nivel se completa solo con sigilo.
- Cada recurso aparece **antes** de la situación donde es útil, en un lugar con historia (por qué está ahí).

## 6. Estructura del nivel (beats)

Ruta principal de oeste a este en ~70 × 45 m. En el arroyo hay dos cruces cercanos (puente expuesto / tronco
entre la maleza) que convergen a los pocos metros: es una elección táctica, no dos niveles.

| # | Zona | Duración | Qué pasa | Enseña / pone a prueba |
|---|---|---|---|---|
| 0 | **Llegada** (cinemática corta) | 30 s | La camioneta del papá llega a la estación, luz del farol, zumbido lejano. Título. | Tono |
| 1 | **Estación 7** (refugio en ruinas, sin techo) | 2–3 min | Exploración segura. Foto de la familia. Garrote en la leñera. Botiquín. Primer objetivo. | Mover, usar, linterna, garrote (contra una puerta trabada) |
| 2 | **El sendero** | 2 min | Un Lurker cruza el sendero a lo lejos (evento con guion) y desaparece. Pasto alto junto al camino. | Esconderse, quietud, leer sonido |
| 3 | **El arroyo** — decisión | 1 min | El puente (rápido, sale al prado del OVNI) o el tronco caído (lento, bosque denso con Lurker B). Zapatilla de Leo junto al puente. | Elección de ruta |
| 4a | **El prado del reflector** (ruta corta) | 3 min | El OVNI barre el claro. Rocas y pastizales como islas de sombra. | Moverse entre sombras, agacharse |
| 4b | **El bosque del norte** (ruta larga) | 4 min | Lurker B patrulla entre arbustos. Piedras para distraer. Caja con bengala. | Distraer, golpe por sorpresa opcional |
| 5 | **El campamento** | 3 min | Lurker A ronda la llegada. Mapa con la ruta a la camioneta. Bengala en la caja. | Combinar: distraer con piedra o bengala, esconderse |
| 6 | **La radio** (clímax) | 2 min | Encender la radio hace ruido: el OVNI y los Lurkers convergen. 20 s de transmisión mientras te escondes cerca o resistes con la bengala. | Todo junto |
| 7 | **Final** | 1 min | La transmisión: la voz de un niño. El reflector cae sobre la camioneta. Negro. "THE HOLLOW". Créditos. Reiniciar. | Cierre |

Checkpoints: inicio de 2, salida del arroyo (cada rama), entrada a 5, inicio de 6.

## 7. Layout, escala y capas

- **Escala**: el papá mide 1.8 m; sendero 2.5 m de ancho; el Lurker ve 14 m con cono de 110°; el reflector
  ilumina un círculo de ~7 m. Las islas de cobertura en el prado están a ≤ 8 m entre sí (siempre hay una
  siguiente sombra al alcance de una carrera corta agachado).
- **Capas de lectura (de cerca a lejos)**: suelo con detalle (pasto, hojas, charcos) → objetos jugables con
  contraste (pickups con brillo tenue, pistas con halo cálido) → cobertura (rocas, troncos, arbustos) →
  paredes del nivel (acantilados en terrazas, árboles densos) → fondo (niebla y siluetas).
- **Guía sin HUD**: postes con pintura naranja del sendero, el farol de la estación visible desde el inicio,
  la luz roja intermitente de la antena de radio visible a lo lejos desde el campamento.
- **Obstáculos**: barranco del arroyo (solo dos cruces), meseta central con acantilado (divide rutas), bordes
  del valle en dos terrazas. Nada de muros invisibles visibles: los límites son geometría.

## 8. Enemigos

**El Lurker**: 2.5 m, delgado, brazos largos asimétricos, garras, piernas digitígradas, ojos rojos.
- Comportamiento: Patrol → Investigate (quietud total 0.9 s, la cabeza gira a saltos) → Search → Chase
  (zig-zag, a cuatro patas cuando acelera) → Attack (se yergue, telegrafiado de 0.45 s, tajo) → Search.
- Sentidos: vista con sospecha gradual; oído vía el sistema de ruido; la linterna encendida multiplica la
  visibilidad del papá.
- Lectura: siempre hace ruido antes de atacar (chasquidos, chillido). Ojos: rojo tenue (calma), naranja
  (sospecha), rojo intenso (caza).

**El OVNI**: nunca se ve, solo su haz y su zumbido. Si te mantiene iluminado con la exposición llena te
lleva (vuelves al checkpoint). Al detectarte alerta a los Lurkers cercanos.

## 9. Animación (calidad objetivo)

Personajes riggeados con esqueleto humanoide y clips reales (no poses procedurales):
idle (con variaciones), walk, jog, sprint, arranque/frenado, giro, crouch idle/walk, golpe (2 variantes),
disparo, lanzar, recoger, usar, reacción al golpe, muerte, levantarse. Capa procedural encima solo para:
mirar hacia objetivos (cabeza), inclinarse al girar, y los **tics del Lurker** (quiebres de cuello, espasmos).

## 10. Audio

Cada evento del juego suena. Música adaptativa en tres estados (calma / tensión cuando algo te nota /
persecución) con transiciones en compás. Ambiente por zona (estación: madera crujiendo; arroyo: agua;
prado: viento abierto; bosque: grillos que se callan cuando un Lurker está cerca).

## 11. UI

Mínima y diegética cuando se pueda: vida como viñeta roja y respiración, indicador de sigilo discreto,
objetivo solo al cambiar, pistas en una tarjeta con la foto/objeto. Táctil: joystick + 4 botones contextuales.

## 12. Criterios de "terminado"

- Se juega de principio a fin sin ayuda del desarrollador, en las dos rutas.
- Ningún objeto flotando, atravesando o sin colisión en la ruta jugable.
- 30 fps estables en iPhone 13 (verificado con Profiler en dispositivo).
- Cada evento con sonido; cada acción con animación; cada recurso con su "por qué está aquí".
