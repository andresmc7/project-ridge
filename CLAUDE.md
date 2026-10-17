# Project Ridge — reglas para Claude Code

Survival-horror de acción/aventura para iOS. Unity 6 LTS, URP, C#.
El diseño vive en el GDD del proyecto en Claude ("Project Ridge — GDD"). La meta del PoC está en `docs/MASTER_SPEC.md` (nivel "The Hollow", el papá solo). Si algo aquí choca con el GDD o el spec, pregunta.

## Hitos del PoC
Trabaja un hito a la vez. Cada uno cierra con las pruebas que le tocan de la sección 22 del spec, ejecutadas de verdad.

| Hito | Contenido |
|---|---|
| M1 | Movimiento + cámara + input (greybox) |
| M2 | Sistema de ruido + percepción + IA del alien (greybox) |
| M3 | Rayo del OVNI y exposición |
| M4 | Combate (garrote + pistola de bengalas), recursos, interacción, pistas |
| M5 | Layout de The Hollow en greybox: dos rutas, objetivos, checkpoints, final |
| M6 | Arte: papá y alien animados, entorno, iluminación |
| M7 | Audio |
| M8 | UI, controles táctiles, rendimiento en iPhone |

En paralelo hay un carril de look dev (una esquina del bosque con arte final) que no bloquea los hitos de sistemas.

## Decisiones fijas
- El PoC tiene un solo humano: el papá. Nada de familia visible ni compañeros.
- Armas: garrote de madera (silencioso, durabilidad) + pistola de bengalas (ruidosa; la bengala arde con luz y ruido y atrae enemigos; a quemarropa aturde; 2–3 en todo el nivel).
- Si el OVNI te atrapa: vuelves al último checkpoint.
- Lurker (el alien): alto, flaco, errático. Quietud total y arranques violentos en zig-zag.
- Solo horizontal.

## Entorno
- Desarrollo en Windows (RTX 4070). Builds de iOS en Mac o en la nube; nunca asumir que hay Mac disponible.
- Targets: iPhone y iPad con A15 o superior (iPhone 13 / iPad equivalente). Base: 30 fps estables. 60 fps opcional en modelos Pro y chips M. Solo horizontal. El iPad entra en el PoC: UI, safe areas y aspect ratios deben funcionar en ambos.
- Unity MCP disponible: úsalo para crear/editar escenas, leer la consola y correr play mode.

## Setup en máquina nueva
La config de git es local (`.git/config`) y no viaja con el repo: repetir en cada máquina después de clonar.

1. Git LFS (una vez por máquina, antes de clonar): `git lfs install`. Clonar normal; si los binarios quedaron como punteros, `git lfs pull`. Verificar con `git lfs ls-files`.
2. Versión de Unity del proyecto: ver `m_EditorVersion` en `ProjectSettings/ProjectVersion.txt` (hoy `6000.3.26f1`). Usar el UnityYAMLMerge de esa versión exacta.
3. Ruta de UnityYAMLMerge (`<ver>` = versión del paso 2):
   - Windows: `C:/Program Files/Unity/Hub/Editor/<ver>/Editor/Data/Tools/UnityYAMLMerge.exe`
   - Mac: `/Applications/Unity/Hub/Editor/<ver>/Unity.app/Contents/Tools/UnityYAMLMerge`
4. Registrar el merge driver `unityyamlmerge` (el nombre que usa `.gitattributes`), en la raíz del repo:
   ```
   git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
   git config merge.unityyamlmerge.driver '"<ruta>" merge -h -p --force %O %B %A %A'
   git config merge.unityyamlmerge.recursive binary
   ```
5. Verificar: `git check-attr merge -- Assets/Scenes/SampleScene.unity` debe decir `unityyamlmerge`, y la ruta debe existir.
6. Al actualizar la versión de Unity del proyecto, actualizar la ruta del driver en cada máquina.

## Reglas de trabajo
- Nunca digas que algo funciona sin haberlo probado. Lo que no se pudo verificar se marca `UNVERIFIED` en BUILD_STATUS.md.
- Commit de checkpoint antes de cualquier cambio grande (refactor, actualización de paquetes, cambio de render pipeline).
- Cambios pequeños y verificables. Después de cada sistema: correr play mode y revisar la consola sin errores.
- No instales paquetes ni assets sin anotarlos en ASSETS.md (fuente, licencia, link). Sin licencia clara, no entra.
- Assets de IA con licencia no comercial = placeholder. Se marcan con prefijo `PH_`.
- Prohibido usar las herramientas `Unity_AssetGeneration_*` del MCP de Unity ni cualquier función que gaste créditos de Unity AI.
- Orden de trabajo: manda este archivo. Sistemas en greybox (M1–M5) + carril de look dev en paralelo. La "Phase 2" de la sección 23 del spec queda reemplazada por eso.
- Carpetas y escenas (`Assets/_Project/`, `Sandbox_Greybox`, `TheHollow`) se crean cuando el hito que las necesita empieza; no antes.

## Arquitectura
- Datos en ScriptableObjects (armas, recursos, enemigos, radios de ruido). Nada de números mágicos en código.
- Sistema de ruido central: `NoiseSystem` emite eventos `NoiseEvent(posición, radio, tipo)`. Todo lo que hace ruido pasa por ahí; toda IA que escucha se suscribe ahí.
- IA de enemigos como máquina de estados explícita, con los estados del spec: Patrol → Investigate → Search → Chase → Attack → Search → Patrol.
- Input con el Input System nuevo: táctil + control (MFi/PS/Xbox) desde el inicio.
- Cámara: Cinemachine, 3/4 alta, ángulo fijo. El jugador no rota la cámara. El personaje sí gira libremente (hacia donde camina, y hacia el objetivo al apuntar o atacar).

## Estructura
```
Assets/_Project/
  Scripts/{Core,Player,AI,Noise,Items,UI,Camera}
  Data/        ScriptableObjects
  Scenes/      Sandbox_Greybox, TheHollow (Prologue queda para el juego completo)
  Art/         Modelos, materiales, texturas propias
  Audio/
  Prefabs/
ThirdParty/    Assets externos, sin modificar
```

## Rendimiento (iOS)
- Iluminación horneada por defecto. Máximo 4 luces dinámicas visibles a la vez: linterna, rayo del OVNI, bengala ardiendo y una luz local (p. ej. farol del refugio). Si se pasa del límite, la luz local se apaga o se hornea.
- Sombras en tiempo real solo de la luz principal (luna, con shadow distance corta ~25–30 m) y de la linterna, para que el papá y el alien proyecten sombra. Las demás luces dinámicas, sin sombras.
- Revisar draw calls y memoria en cada hito de look dev con el Profiler.
- LODs en vegetación y rocas.

## Archivos de estado
- docs/MASTER_SPEC.md: meta y pruebas de aceptación del PoC.
- BUILD_STATUS.md: qué compila, qué se probó, dónde y cuándo.
- ASSETS.md: inventario y licencias de todo asset externo.
