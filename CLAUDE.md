# Project Ridge — reglas para Claude Code

Survival-horror de acción/aventura para iOS. Unity 6 LTS, URP, C#.
El diseño vive en el GDD del proyecto en Claude ("Project Ridge — GDD"). Si algo aquí choca con el GDD, pregunta.

## Entorno
- Desarrollo en Windows (RTX 4070). Builds de iOS en Mac o en la nube; nunca asumir que hay Mac disponible.
- Target: iPhone 13 (A15) o superior, 30fps estables, horizontal.
- Unity MCP disponible: úsalo para crear/editar escenas, leer la consola y correr play mode.

## Reglas de trabajo
- Nunca digas que algo funciona sin haberlo probado. Lo que no se pudo verificar se marca `UNVERIFIED` en BUILD_STATUS.md.
- Commit de checkpoint antes de cualquier cambio grande (refactor, actualización de paquetes, cambio de render pipeline).
- Cambios pequeños y verificables. Después de cada sistema: correr play mode y revisar la consola sin errores.
- No instales paquetes ni assets sin anotarlos en ASSETS.md (fuente, licencia, link). Sin licencia clara, no entra.
- Assets de IA con licencia no comercial = placeholder. Se marcan con prefijo `PH_`.

## Arquitectura
- Datos en ScriptableObjects (armas, recursos, enemigos, radios de ruido). Nada de números mágicos en código.
- Sistema de ruido central: `NoiseSystem` emite eventos `NoiseEvent(posición, radio, tipo)`. Todo lo que hace ruido pasa por ahí; toda IA que escucha se suscribe ahí.
- IA de enemigos como máquina de estados explícita: Patrol → Suspicious → Investigate → Hunt → LostTrack → Return.
- Input con el Input System nuevo: táctil + control (MFi/PS/Xbox) desde el inicio.
- Cámara: Cinemachine, 3/4 alta fija, sin rotación del jugador.

## Estructura
```
Assets/_Project/
  Scripts/{Core,Player,AI,Noise,Items,UI,Camera}
  Data/        ScriptableObjects
  Scenes/      Prologue, Level01_Forest, Sandbox_Greybox
  Art/         Modelos, materiales, texturas propias
  Audio/
  Prefabs/
ThirdParty/    Assets externos, sin modificar
```

## Rendimiento (iOS)
- Iluminación horneada por defecto. Máximo 3 luces en tiempo real a la vez (linterna, rayo del OVNI, fogata).
- Revisar draw calls y memoria en cada hito de look dev con el Profiler.
- LODs en vegetación y rocas. Sin sombras en tiempo real salvo la linterna.

## Archivos de estado
- BUILD_STATUS.md: qué compila, qué se probó, dónde y cuándo.
- ASSETS.md: inventario y licencias de todo asset externo.
