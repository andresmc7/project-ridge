# BUILD_STATUS

Qué compila, qué se probó, dónde y cuándo. Lo no verificado va marcado `UNVERIFIED`.

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
