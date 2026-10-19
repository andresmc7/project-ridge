# BUILD_STATUS

Qué compila, qué se probó, dónde y cuándo. Lo no verificado va marcado `UNVERIFIED`.

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
