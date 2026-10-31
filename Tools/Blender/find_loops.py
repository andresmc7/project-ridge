# Busca ciclos de locomoción limpios (2 pasos) en tomas BVH de 100STYLE, en Python puro.
# Uso: python3 -I find_loops.py <bvh> <first> <last>
import sys, math, numpy as np

def parse_bvh(path):
    lines = open(path).read().split('\n')
    i = 0; joints = []; stack = []; chan_count = 0
    while not lines[i].startswith('MOTION'):
        t = lines[i].split()
        if t and t[0] in ('ROOT','JOINT'):
            j = {'name': t[1], 'parent': stack[-1] if stack else None, 'offset': None, 'channels': [], 'ci': chan_count}
            joints.append(j); stack.append(len(joints)-1)
        elif t and t[0] == 'End':
            j = {'name': joints[stack[-1]]['name']+'_End', 'parent': stack[-1], 'offset': None, 'channels': [], 'ci': chan_count}
            joints.append(j); stack.append(len(joints)-1)
        elif t and t[0] == 'OFFSET':
            joints[stack[-1]]['offset'] = np.array(list(map(float, t[1:4])))
        elif t and t[0] == 'CHANNELS':
            n = int(t[1]); joints[stack[-1]]['channels'] = t[2:2+n]; chan_count += n
        elif t and t[0] == '}':
            stack.pop()
        i += 1
    nframes = int(lines[i+1].split()[1]); ft = float(lines[i+2].split()[2])
    data = np.array([list(map(float, l.split())) for l in lines[i+3:i+3+nframes] if l.strip()])
    return joints, data, ft

def rot(axis, deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    if axis == 'X': return np.array([[1,0,0],[0,c,-s],[0,s,c]])
    if axis == 'Y': return np.array([[c,0,s],[0,1,0],[-s,0,c]])
    return np.array([[c,-s,0],[s,c,0],[0,0,1]])

def fk(joints, frame):
    pos = {}; mats = {}
    for idx, j in enumerate(joints):
        R = np.eye(3); T = np.zeros(3)
        for k, ch in enumerate(j['channels']):
            v = frame[j['ci']+k]
            if ch.endswith('position'): T['XYZ'.index(ch[0])] = v
            else: R = R @ rot(ch[0], v)
        if j['parent'] is None:
            P = T; M = R
        else:
            pm = mats[j['parent']]; pp = pos[j['parent']]
            P = pp + pm @ (j['offset'] + T); M = pm @ R
        pos[idx] = P; mats[idx] = M
    return pos

def main():
    path, first, last = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
    joints, data, ft = parse_bvh(path)
    names = [j['name'] for j in joints]
    li, ri = names.index('LeftToe'), names.index('RightToe')
    hips = names.index('Hips')
    frames = range(first, last+1)
    L = []; R = []; H = []
    for f in frames:
        p = fk(joints, data[f]); L.append(p[li]); R.append(p[ri]); H.append(p[hips])
    L, R, H = np.array(L), np.array(R), np.array(H)
    # contactos: pie más bajo y con poca velocidad horizontal
    def contacts(F):
        y = F[:,1]; v = np.linalg.norm(np.diff(F[:, [0,2]], axis=0), axis=1)/ft
        c = np.zeros(len(F), bool); thr = np.percentile(y, 20) + 3.0
        c[1:] = (y[1:] < thr) & (v < 90.0)   # cm/s
        print(f"  toe y min {y.min():.1f} p20 {np.percentile(y,20):.1f} max {y.max():.1f}; v median {np.median(v):.0f} cm/s; contact frac {c.mean():.2f}")
        return c
    cl, cr = contacts(L), contacts(R)
    # inicio de apoyo izquierdo (flanco de subida)
    starts = [i for i in range(1, len(cl)) if cl[i] and not cl[i-1]]
    best = None
    for a_i, a in enumerate(starts[:-2]):
        b = starts[a_i+2]  # dos pasos después (mismo pie): un ciclo completo
        if b - a < 20 or b - a > 120: continue
        # error de pose: ángulos relativos aproximados por posiciones relativas a la cadera
        rel = lambda i: np.concatenate([(L[i]-H[i]), (R[i]-H[i])])
        err = np.linalg.norm(rel(a) - rel(b))
        # velocidad media del ciclo (cm/s) en el plano
        d = np.sum(np.linalg.norm(np.diff(H[a:b+1][:, [0,2]], axis=0), axis=1)); speed = d / ((b-a)*ft)
        if best is None or err < best[0]: best = (err, a+first, b+first, speed)
    if best is None: print("no cycles; starts:", len(starts)); return
    err, a, b, speed = best
    print(f"best cycle frames {a}..{b} ({b-a} f, {((b-a)*ft):.2f}s) pose err {err:.1f} cm, speed {speed/100:.2f} m/s")
    # top 5 alternativas
    print("alts:")
    cands = []
    for a_i, a0 in enumerate(starts[:-2]):
        b0 = starts[a_i+2]
        if b0 - a0 < 20 or b0 - a0 > 120: continue
        rel = lambda i: np.concatenate([(L[i]-H[i]), (R[i]-H[i])])
        e = np.linalg.norm(rel(a0) - rel(b0)); d = np.sum(np.linalg.norm(np.diff(H[a0:b0+1][:, [0,2]], axis=0), axis=1)); sp = d/((b0-a0)*ft)
        chord = np.linalg.norm((H[b0]-H[a0])[[0,2]]); e = e + 40.0*(1.0 - chord/max(d,1e-6))
        cands.append((e, a0+first, b0+first, sp))
    for e, a0, b0, sp in sorted(cands)[:6]: print(f"  {a0}..{b0} err {e:.1f} speed {sp/100:.2f}")
main()
