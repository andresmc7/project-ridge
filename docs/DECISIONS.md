# Decisiones tomadas durante el POLISH PASS

Registro de decisiones ambiguas resueltas en modo autónomo (ver `docs/POLISH_PASS.md`, sección 0). Formato: fecha — decisión — por qué.

- 2026-10-20 — Se descarga **100STYLE** (Mason, Starke, Komura 2022; Zenodo 8127870; **CC BY 4.0**) en `_incoming/100style/` solo el `100STYLE.zip` con los BVH originales (1.47 GB), no los datos etiquetados (14.7 GB). — Para animación solo hacen falta los BVH; los datos etiquetados son para entrenar redes.
- 2026-10-20 — `ProjectSettings.runInBackground` queda en 1 **sin commitear** mientras dure el pase. — Las pruebas autónomas en play mode necesitan que Unity siga corriendo sin foco; se restaura a 0 antes del build (P9).
- 2026-10-20 — La referencia `target_look.png` muestra a la familia; se ignora ese detalle. — `CLAUDE.md` fija que el PoC tiene un solo humano (el papá).
- 2026-10-20 — Velocidades naturales medidas por la zancada (el pie apoyado se desliza hacia atrás a la velocidad del cuerpo), no por root motion. — Los clips de Quaternius vienen "in place": no tienen desplazamiento de raíz que medir.
- 2026-10-20 — Persecución del Lurker 4.4 → 3.9 m/s e investigación 1.4 → 1.25 m/s. — El sprint del papá bajó a 3.6 m/s (velocidad natural del clip); así el Lurker sigue un 8% más rápido (antes 5%) y sus clips no pasan de 1.1x.
- 2026-10-20 — Recoger del suelo con `Farm_Harvest` (UAL2) a 1.6x. — Es el único clip disponible que se agacha hasta el suelo con rodillas flexionadas. Es un clip de acción, no de locomoción: la regla de 0.85–1.1x no aplica. Se reemplaza por "Picking Up" de Mixamo si llega.
- 2026-10-20 — El garrote se desenfunda con IK de la mano a la funda (arco de 0.35 s) en vez de un clip. — No hay clip de desenfundar en UAL1/UAL2; los de Mixamo ("Draw Sword") se usarán en P7 si llegan.
- 2026-10-20 — Sin garrote, ATTACK no hace nada hasta P1 (empujar/esquivar). — Se eliminó la fabricación con chatarra (sección 3.3).
- 2026-10-20 — Las reglas de armas y recursos de la sección 3.3 (aturdir sin matar, derribo, límites de inventario, pistola de bengalas) se implementan en P1, junto con el botón contextual. — Ninguna fase las nombra; P1 es donde el botón ACCIÓN/OBJETO las necesita.
- 2026-10-20 — 100STYLE (BVH) se usará en P7 tras convertir a FBX con Blender (P6 instala Blender). Estilos útiles: `Neutral`, `InTheDark`, `Followed`, `Crouched`, `StartStop`, `Rushed`, `LimpLeft` (papá) y `Zombie`, `Dinosaur` (Lurker). — Unity no importa BVH directamente.
