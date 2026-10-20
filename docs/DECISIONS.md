# Decisiones tomadas durante el POLISH PASS

Registro de decisiones ambiguas resueltas en modo autónomo (ver `docs/POLISH_PASS.md`, sección 0). Formato: fecha — decisión — por qué.

- 2026-10-20 — Se descarga **100STYLE** (Mason, Starke, Komura 2022; Zenodo 8127870; **CC BY 4.0**) en `_incoming/100style/` solo el `100STYLE.zip` con los BVH originales (1.47 GB), no los datos etiquetados (14.7 GB). — Para animación solo hacen falta los BVH; los datos etiquetados son para entrenar redes.
- 2026-10-20 — `ProjectSettings.runInBackground` queda en 1 **sin commitear** mientras dure el pase. — Las pruebas autónomas en play mode necesitan que Unity siga corriendo sin foco; se restaura a 0 antes del build (P9).
- 2026-10-20 — La referencia `target_look.png` muestra a la familia; se ignora ese detalle. — `CLAUDE.md` fija que el PoC tiene un solo humano (el papá).
