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

## Quaternius (personajes y animaciones)

Fuente: Quaternius (https://quaternius.com), descargados por el usuario desde itch.io (versiones [Standard], gratuitas) el 2026-10-19. Licencia: **CC0 1.0** (dominio público), según `License.txt` / `License_Standard.txt` incluidos en cada paquete y copiados junto a los archivos. Sin modificar, en `Assets/ThirdParty/Quaternius/` (solo los archivos usados; los zips originales quedan fuera del repo en `_incoming/`). Uso: el papá (cuerpo, pelo, barba, cejas; la ropa se genera en el proyecto) y el Lurker (mismo esqueleto deformado) con animaciones reales.

| Asset | Contenido usado | Link |
|---|---|---|
| Universal Base Characters [Standard] | `Superhero_Male_FullBody.fbx`, texturas de piel/ojos/pelo, `Hair_Beard`, `Hair_SimpleParted`, `Eyebrows_Regular` (rigged to head bone) | https://quaternius.itch.io/universal-base-characters |
| Universal Animation Library [Standard] | `UAL1_Standard.fbx` (sin root motion) | https://quaternius.itch.io/universal-animation-library |
| Universal Animation Library 2 [Standard] | `UAL2_Standard.fbx` (sin root motion; incluye locomoción de zombi) | https://quaternius.itch.io/universal-animation-library-2 |

**Poly Haven — agregados 2026-10-19** (licencia CC0, 1k, MD5 verificado). Uso: nivel The Hollow (cabaña, puente, rocas, camioneta) y ropa del papá.

| Asset | Tipo | Autor(es) | Link |
|---|---|---|---|
| `weathered_brown_planks` | Textura | Dimitrios Savva, Rico Cilliers | https://polyhaven.com/a/weathered_brown_planks |
| `mossy_rock` | Textura | Rob Tuytel | https://polyhaven.com/a/mossy_rock |
| `rusty_metal_02` | Textura | Rob Tuytel | https://polyhaven.com/a/rusty_metal_02 |
| `denim_fabric` | Textura | Rob Tuytel | https://polyhaven.com/a/denim_fabric |
| `leather_red_02` (variante de color `coll1`, guardada como `_diff_1k.jpg`) | Textura | Rob Tuytel | https://polyhaven.com/a/leather_red_02 |
| `fabric_pattern_07` (variante de color `col_1`, guardada como `_diff_1k.jpg`) | Textura | Rob Tuytel | https://polyhaven.com/a/fabric_pattern_07 |

Los pinos/abetos son mallas procedurales propias (generador en `Assets/_Project/Scripts/Editor/`); sus ramas usan el atlas `twig` de `fir_tree_01`. El modelo de `fir_tree_01` (7.8M polígonos) no se descargó.

## Kenney (audio)

Fuente: Kenney "Impact Sounds" 1.0 (https://kenney.nl/assets/impact-sounds), descargado el 2026-10-20 desde kenney.nl. Licencia: **CC0 1.0** (dominio público), según `License.txt` incluido y copiado junto a los archivos. Sin modificar, en `Assets/ThirdParty/Kenney/ImpactSounds/` (solo los archivos usados; el zip queda fuera del repo en `_incoming/`). Uso: pisadas del papá y del Lurker (pasto, madera, piedra), golpes de cuerpo en combate y escombros que salen volando (madera, metal).

| Archivos | Uso |
|---|---|
| `footstep_grass_000–004`, `footstep_wood_000–004`, `footstep_concrete_000–004` | Pisadas |
| `impactPunch_heavy_000–004`, `impactSoft_heavy_000–004` | Golpes de cuerpo |
| `impactWood_light_000–004`, `impactPlank_medium_000–004`, `impactMetal_light_000–004` | Escombros |

## MakeHuman / MPFB2 (el papá, POLISH P6)

Herramientas (no entran al proyecto, viven en `_incoming/`, ignorado por git): Blender 4.2.9 LTS portable (GPL; https://download.blender.org/release/Blender4.2/, sha256 `223cd22a…99cb` verificado) y la extensión **MPFB 2.0.18** (GPL v3; https://extensions.blender.org/add-ons/mpfb/, sha256 `c075b3e9…2dad` verificado). La salida de MPFB/MakeHuman no hereda la GPL: según el FAQ oficial de MPFB, los assets base (malla, targets, pieles) son **CC0** y los modelos hechos con MPFB pueden usarse en un juego de código cerrado (https://static.makehumancommunity.org/mpfb/faq/use_in_closed_source.html; cambio de licencia de 2020: https://static.makehumancommunity.org/oldsite/faq/what_changed_regarding_the_license_in_2020.html).

Generado con `Tools/Blender/make_father.py` (fuente de verdad; re-ejecutable) → `Assets/ThirdParty/MakeHuman/Father/` (FBX + texturas a 1k + `father_mh.json`). 14 462 triángulos, rig `game_engine` (Humanoid en Unity). Barba de tres días y barro pintados por el script sobre las texturas. Fecha: 2026-10-21.

| Asset | Pack | Autor | Licencia | Link del pack |
|---|---|---|---|---|
| Malla base MakeHuman, piel `middleage_caucasian_male`, ojos `low-poly` + material `brown`, ceja `eyebrow001`, pelo `short02` | makehuman_system_assets | MakeHuman team (Data Collection AB, Joel Palmius, Jonas Hauquier) | CC0 | https://files.makehumancommunity.org/asset_packs/makehuman_system_assets/makehuman_system_assets_cc0.zip |
| Ropa `male_casualsuit05` (chaqueta, camisa, jeans) | makehuman_system_assets | MakeHuman team | CC0 | (mismo zip) |
| Botas `toigo_ankle_boots_male` | shoes01 | MargaretToigo | CC0 | https://files.makehumancommunity.org/asset_packs/shoes01/shoes01_cc0.zip |

Packs descargados pero **no usados** (no entran al juego): shirts02 y pants02 (CC-BY; si se usaran habría que acreditar a sus autores).

## 100STYLE (animación del papá, POLISH P7)

**100STYLE Dataset** — Ian Mason (MIT), Sebastian Starke (Meta), Taku Komura (HKU). https://zenodo.org/records/8127870 — **CC BY 4.0** (atribución obligatoria: va en `Assets/ThirdParty/100STYLE/CREDITS.txt` y en la pantalla de créditos del juego, pausa → CREDITS y al final de la demo; texto en `Assets/_Project/Data/Credits.txt`). Descargado el `100STYLE.zip` (BVH) a `_incoming/100style/`. Cambios: segmentos recortados de las tomas y convertidos a FBX con `Tools/Blender/convert_100style.py` → `Assets/ThirdParty/100STYLE/`. Fecha: 2026-10-21.

| Clip | Toma (cuadros a 60 fps) | Uso |
|---|---|---|
| `S100_WalkStart` | Neutral_FW 5942–6012 | Arranque caminando |
| `S100_WalkStop` | Neutral_FW 7748–7831 | Frenada caminando |
| `S100_RunStop` | Neutral_FR 4231–4311 | Frenada trotando/corriendo |
| `S100_TurnA`, `S100_TurnB` | Neutral_FW 406–461, 5919–5971 | Giro en el sitio (pasos) |
| `S100_Pivot` | Neutral_FR 409–469 | Pivote de 180° en carrera |
| `S100_TenseIdle` | BentForward_ID 600–1200 | Idle tenso (encorvado) |
| `S100_LookAround` | Followed_ID 640–830 | Mirar atrás asustado tras 6 s quieto |

## Música y stingers (POLISH P8)

FreePD cerró (2026); todo sale de **OpenGameArt** con licencia **CC0** verificada en la página de cada asset. Sin modificar salvo `piano_sting` (era un contenedor MP4/M4A con extensión .mp3: se pasó a WAV con Blender, mismo audio). En `Assets/ThirdParty/OpenGameArt/Music/`. Fecha: 2026-10-21.

| Archivo | Asset (autor) | Licencia | Link | Uso |
|---|---|---|---|---|
| `abandoned_passages_freezer.ogg` | Abandoned passages (horror ambience loop) — congusbongus | CC0 | https://opengameart.org/content/abandoned-passages-horror-ambience-loop | Capa Explorar |
| `srg774_pulse.mp3` | Dark Sci-Fi Audio Pack: Pulse — SRG774 | CC0 | https://opengameart.org/content/dark-sci-fi-audio-pack | Capa Tensión (pulso como latido) |
| `srg774_urgent.mp3` | Dark Sci-Fi Audio Pack: Urgent — SRG774 | CC0 | (mismo pack) | Capa Persecución |
| `srg774_transmission.mp3` | Dark Sci-Fi Audio Pack: Transmission — SRG774 | CC0 | (mismo pack) | Stinger del final |
| `srg774_victory.mp3` | Dark Sci-Fi Audio Pack: Victory — SRG774 | CC0 | (mismo pack) | Stinger de checkpoint |
| `congusbongus_abyss.ogg` | String and piano horror stings: abyss — congusbongus | CC0 | https://opengameart.org/content/string-and-piano-horror-stings | Stingers de detección y captura |
| `congusbongus_piano_sting.wav` | String and piano horror stings: piano_sting — congusbongus | CC0 | (mismo) | Stinger de pista |

Descartado: "Outrunners" (OOG37): la página dice CC0 pero el aviso dice "Copyright 2022 Two Litre Falls Music… for public use"; licencia no clara → no entra.

## Tipografía (POLISH P8)

**Barlow Condensed SemiBold** — The Barlow Project Authors (https://github.com/jpt/barlow), **SIL Open Font License 1.1** (`OFL.txt` junto al archivo). Descargada de https://github.com/google/fonts/tree/main/ofl/barlowcondensed. En `Assets/ThirdParty/Fonts/BarlowCondensed/`. Única fuente del HUD. Fecha: 2026-10-21.

Copias normalizadas (`Assets/_Project/Audio/Normalized/*_n.ogg`): derivadas de los clips CC0 de arriba (Kenney, OpenGameArt) y de los sintetizados del proyecto, solo con ganancia para llegar a su objetivo de loudness (pico limitado a −1 dBFS). Las genera `Tools/Blender/normalize_audio.py` con la lista que escribe `MusicBuilder` (P8). Misma licencia que el original.

## Propios del proyecto (sin licencia externa)

Generados por código del proyecto; no son de terceros:

- `Assets/_Project/Audio/Generated/*.wav`: efectos sintetizados por `Scripts/Editor/SoundSynth.cs` (pasos alternativos, golpes del garrote, bengala, Lurker, ambiente, latido, stingers de alerta y checkpoint, etc.). Sus copias normalizadas están en `Audio/Normalized/`.
- `Assets/_Project/Art/Meshes/Chunky/*`: mallas generadas por los builders (`ChunkyMeshGenerator`, `MeshDecimator`). Las `PolyHaven_*` son versiones reducidas de rocas de Poly Haven (CC0, ver arriba): `rock_moss_set_01/02`, `boulder_01`.

**Prefijos**: `PH_` queda reservado para placeholders de IA con licencia no comercial (CLAUDE.md); hoy **no hay ninguno** en el proyecto. Lo derivado de Poly Haven usa `PolyHaven_`. El 2026-10-29 se renombraron 28 mallas `PH_Hollow*` → `PolyHaven_Hollow*`: eran rocas CC0 de Poly Haven, no placeholders. Se renombraron con `AssetDatabase.RenameAsset`, que conserva el GUID: la escena y los prefabs mantienen las 1055 referencias de render y las 902 de colisión.
