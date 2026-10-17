# BUILD_STATUS

Qué compila, qué se probó, dónde y cuándo. Lo no verificado va marcado `UNVERIFIED`.

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
