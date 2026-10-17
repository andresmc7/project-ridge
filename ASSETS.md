# ASSETS — inventario y licencias

Todo paquete o asset externo se anota aquí antes de entrar al proyecto. Sin licencia clara, no entra.

## Paquetes (Unity Package Manager)

| Paquete | Versión | Fuente | Licencia | Uso | Fecha |
|---|---|---|---|---|---|
| `com.unity.ai.assistant` (Assistant / Unity MCP) | 2.20.0-pre.2 (preview) | Registro oficial de Unity — https://docs.unity3d.com/Packages/com.unity.ai.assistant@latest | Unity Terms of Service (https://unity.com/legal), según `LICENSE.md` del paquete | Solo editor: servidor MCP para controlar el editor desde Claude Code. Sin prueba ni suscripción de Unity AI. | 2026-10-16 |
| `com.unity.cinemachine` | 3.1.7 | Registro oficial de Unity — https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1 | Unity Companion License (http://www.unity3d.com/legal/licenses/Unity_Companion_License), según `LICENSE.md` del paquete | Cámara 3/4 del jugador (M1). Trae como dependencia `com.unity.splines` 2.x (misma licencia). | 2026-10-17 |

Los paquetes que trae la plantilla URP de Unity (`Packages/manifest.json` al crear el proyecto) no se listan uno por uno; también se rigen por los Unity Terms of Service.

## Assets de arte / audio

Fuente: Poly Haven (https://polyhaven.com). Licencia: **CC0** para todo el sitio (https://polyhaven.com/license), sin atribución obligatoria; igual se registra el autor. Resolución 1k, descargados vía `api.polyhaven.com` con verificación MD5, sin modificar, en `Assets/ThirdParty/PolyHaven/`. Materiales y texturas empaquetadas derivadas viven en `Assets/_Project/Art/`. Fecha: 2026-10-17. Uso: carril de look dev (esquina del bosque).

| Asset | Tipo | Autor(es) | Link |
|---|---|---|---|
| `forest_ground_04` | Textura | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/forest_ground_04 |
| `mud_forest` | Textura | eye-candy.xyz | https://polyhaven.com/a/mud_forest |
| `rocky_trail` | Textura | Amal Kumar | https://polyhaven.com/a/rocky_trail |
| `forest_leaves_04` | Textura | Rob Tuytel | https://polyhaven.com/a/forest_leaves_04 |
| `pine_bark` | Textura | Dimitrios Savva | https://polyhaven.com/a/pine_bark |
| `rock_moss_set_01` | Modelo | Kless Gyzen | https://polyhaven.com/a/rock_moss_set_01 |
| `rock_moss_set_02` | Modelo | Kless Gyzen | https://polyhaven.com/a/rock_moss_set_02 |
| `boulder_01` | Modelo | Rico Cilliers | https://polyhaven.com/a/boulder_01 |
| `fern_02` | Modelo | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/fern_02 |
| `tree_stump_01` | Modelo | Rob Tuytel | https://polyhaven.com/a/tree_stump_01 |
| `dead_tree_trunk` | Modelo | Rob Tuytel | https://polyhaven.com/a/dead_tree_trunk |
| `dry_branches_medium_01` | Modelo | Rico Cilliers | https://polyhaven.com/a/dry_branches_medium_01 |
| `fir_tree_01` (solo texturas `twig`: diff, alpha, nor_gl, rough) | Textura | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/fir_tree_01 |

Los pinos/abetos son mallas procedurales propias (generador en `Assets/_Project/Scripts/Editor/`); sus ramas usan el atlas `twig` de `fir_tree_01`. El modelo de `fir_tree_01` (7.8M polígonos) no se descargó.
