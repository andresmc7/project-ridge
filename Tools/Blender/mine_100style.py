# Busca segmentos útiles en tomas de 100STYLE (BVH, 60 fps, cm, Y arriba) a partir de la trayectoria de la cadera:
# arranques, frenadas, giros en el sitio y pivotes de 180° en carrera. Escribe un JSON con los candidatos.
#
# Uso: python -I Tools/Blender/mine_100style.py <carpeta 100STYLE> <salida.json> Estilo_Toma [Estilo_Toma ...]
#   p. ej. StartStop_FW StartStop_FR Neutral_TR1 InTheDark_ID
#
# Convenciones de la toma: el personaje mira hacia +Z con yaw 0 (la punta del pie está en +Z en la pose de enlace).

import json
import math
import os
import sys

import numpy as np


def load_root(path):
    """Posición (m, en XZ) y yaw (grados) de la cadera por cuadro."""
    with open(path, encoding="utf-8", errors="ignore") as f:
        lines = f.read().splitlines()
    i = next(k for k, l in enumerate(lines) if l.strip() == "MOTION")
    dt = float(lines[i + 2].split(":")[1])
    rows = [l.split(None, 7)[:6] for l in lines[i + 3:] if l.strip()]
    a = np.array(rows, dtype=np.float64)
    pos = a[:, [0, 2]] / 100.0
    yaw = np.unwrap(np.radians(a[:, 3]))
    return pos, np.degrees(yaw), dt


def smooth(x, n):
    if n <= 1:
        return x
    k = np.ones(n) / n
    pad = np.pad(x, ((n // 2, n - 1 - n // 2),) + ((0, 0),) * (x.ndim - 1), mode="edge")
    if x.ndim == 1:
        return np.convolve(pad, k, mode="valid")
    return np.stack([np.convolve(pad[:, j], k, mode="valid") for j in range(x.shape[1])], 1)


def analyse(pos, yaw, dt):
    fps = 1.0 / dt
    p = smooth(pos, int(0.2 * fps))
    v = np.gradient(p, dt, axis=0)
    speed = np.linalg.norm(v, axis=1)
    heading = np.degrees(np.unwrap(np.arctan2(v[:, 0], v[:, 1])))
    yaw_s = smooth(yaw, int(0.15 * fps))
    yaw_rate = np.gradient(yaw_s, dt)
    return speed, heading, yaw_s, yaw_rate


def segments(name, pos, yaw, dt):
    fps = 1.0 / dt
    speed, heading, yaw_s, yaw_rate = analyse(pos, yaw, dt)
    n = len(speed)
    out = []
    sec = lambda s: int(round(s * fps))

    # Arranques y frenadas: quieto >= 0.4 s y luego en marcha estable (o al revés).
    still = speed < 0.12
    k = sec(0.4)
    for i in range(k, n - sec(2.0)):
        if still[i - k:i].all() and not still[i] :
            j = i
            while j < n - 1 and speed[j] < 0.9 * np.median(speed[i + sec(1.2):i + sec(2.0)]):
                j += 1
            plateau = float(np.median(speed[i + sec(1.2):i + sec(2.0)]))
            if plateau < 0.6:
                continue
            facing = abs(((yaw_s[j] - heading[j]) + 180) % 360 - 180)
            out.append({"kind": "start", "take": name, "start": i - sec(0.15), "end": j + sec(0.35), "speed": plateau, "facing_err": facing,
                        "yaw_change": float(yaw_s[j] - yaw_s[i])})
    for i in range(sec(2.0), n - k):
        if still[i:i + k].all() and not still[i - 1]:
            j = i
            plateau = float(np.median(speed[i - sec(2.0):i - sec(1.2)]))
            if plateau < 0.6:
                continue
            while j > 0 and speed[j] < 0.9 * plateau:
                j -= 1
            facing = abs(((yaw_s[j] - heading[j]) + 180) % 360 - 180)
            out.append({"kind": "stop", "take": name, "start": j - sec(0.2), "end": i + sec(0.35), "speed": plateau, "facing_err": facing,
                        "yaw_change": float(yaw_s[i] - yaw_s[j])})

    # Giros en el sitio: el yaw cambia >= 60° mientras la cadera casi no se desplaza.
    turning = (np.abs(yaw_rate) > 35.0) & (speed < 0.35)
    i = 0
    while i < n:
        if turning[i]:
            j = i
            while j < n and turning[j]:
                j += 1
            total = float(yaw_s[min(j, n - 1)] - yaw_s[i])
            disp = float(np.linalg.norm(pos[min(j, n - 1)] - pos[i]))
            if 60.0 <= abs(total) <= 220.0 and disp < 0.45 and (j - i) * dt <= 2.0:
                out.append({"kind": "turn", "take": name, "start": i - sec(0.12), "end": j + sec(0.2), "yaw_change": total,
                            "duration": (j - i) * dt, "displacement": disp})
            i = j
        else:
            i += 1

    # Pivote en marcha: la dirección de avance cambia >= 140° en <= 1.2 s, con velocidad antes y después.
    w = sec(1.2)
    i = sec(0.5)
    while i < n - w - sec(0.8):
        if speed[i] > 1.0:
            dh = heading[i + w] - heading[i]
            if abs(dh) >= 140.0 and speed[i + w] > 0.9:
                # Ajusta el inicio al punto de menor velocidad (el apoyo del giro).
                low = i + int(np.argmin(speed[i:i + w]))
                out.append({"kind": "pivot", "take": name, "start": low - sec(0.45), "end": low + sec(0.55), "heading_change": float(dh),
                            "yaw_change": float(yaw_s[low + sec(0.55)] - yaw_s[low - sec(0.45)]), "speed_in": float(speed[i]), "speed_out": float(speed[i + w])})
                i += w
                continue
        i += 1
    for s in out:
        s["start"] = int(max(0, s["start"]))
        s["end"] = int(min(n - 1, s["end"]))
        s["fps"] = fps
    return out


def main():
    root, out_path = sys.argv[1], sys.argv[2]
    result = []
    for take in sys.argv[3:]:
        style = take.split("_")[0]
        path = os.path.join(root, style, take + ".bvh")
        pos, yaw, dt = load_root(path)
        segs = segments(take, pos, yaw, dt)
        result.extend(segs)
        kinds = {}
        for s in segs:
            kinds[s["kind"]] = kinds.get(s["kind"], 0) + 1
        print(take, len(pos), "frames", kinds)
    with open(out_path, "w") as f:
        json.dump(result, f, indent=1)


if __name__ == "__main__":
    main()
