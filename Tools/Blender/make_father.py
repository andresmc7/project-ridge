# Genera al papá con MPFB2 (MakeHuman para Blender) en modo headless y lo exporta a FBX para Unity.
#
# Uso (Blender 4.2 portable con MPFB 2.0.x instalado y los assets CC0/CC-BY de MakeHuman en su carpeta de datos):
#   (sin --factory-startup, para que cargue la extensión)
#   blender -b --python Tools/Blender/make_father.py -- <carpeta_salida>
#
# Salida: Father_MH.fbx (rig "game_engine", malla con submallas por pieza) + texturas a 1k + father_mh.json
# con el conteo de triángulos y el mapa pieza -> texturas. El generador de Unity (CharacterBuilder) lee eso.
#
# Assets usados (ver ASSETS.md):
#   base MakeHuman, piel middleage_caucasian_male, ojos low-poly, ceja eyebrow001, pelo short02,
#   ropa male_casualsuit05 (chaqueta, camisa, jeans) -> CC0 (makehuman_system_assets_cc0)
#   botas toigo_ankle_boots_male -> CC0 (shoes01)

import bpy
import os
import sys
import json
import numpy as np

from bl_ext.user_default.mpfb.services.humanservice import HumanService
from bl_ext.user_default.mpfb.services.assetservice import AssetService
from bl_ext.user_default.mpfb.services.targetservice import TargetService

OUT = os.path.abspath(sys.argv[sys.argv.index("--") + 1]) if "--" in sys.argv else os.path.abspath("FatherOut")
os.makedirs(OUT, exist_ok=True)
TEX = 1024

# 38 años (edad MH: 0.5 = 25 años, 1.0 = 90), complexión media, algo de músculo de trabajo.
MACRO = {
    "gender": 1.0, "age": 0.6, "muscle": 0.56, "weight": 0.55, "proportions": 0.62, "height": 0.55,
    "cupsize": 0.5, "firmness": 0.5,
    "race": {"caucasian": 0.8, "african": 0.1, "asian": 0.1},
}

# Pieza -> (subcarpeta, archivo mhclo, presupuesto de triángulos). Total <= 15k con el cuerpo.
PIECES = [
    ("eyes", "low-poly/low-poly.mhclo", 300),
    ("eyebrows", "eyebrow001/eyebrow001.mhclo", 200),
    ("hair", "short02/short02.mhclo", 1800),
    ("clothes", "male_casualsuit05/male_casualsuit05.mhclo", 5600),
    ("clothes", "toigo_ankle_boots_male/toigo_ankle_boots_male.mhclo", 1300),
]
BODY_BUDGET = 5400
SKIN = "skins/middleage_caucasian_male/middleage_caucasian_male.mhmat"


def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def apply_modifier(obj, name):
    bpy.context.view_layer.objects.active = obj
    with bpy.context.temp_override(object=obj, active_object=obj):
        bpy.ops.object.modifier_apply(modifier=name)


def decimate(obj, budget):
    tris = tri_count(obj)
    if tris <= budget:
        return
    m = obj.modifiers.new("Decimate", "DECIMATE")
    m.decimate_type = "COLLAPSE"
    m.ratio = budget / tris
    m.use_collapse_triangulate = True
    # El decimado debe ir antes que el armature para no hornear la pose.
    while obj.modifiers.find("Decimate") > 0:
        with bpy.context.temp_override(object=obj, active_object=obj):
            bpy.ops.object.modifier_move_up(modifier="Decimate")
    apply_modifier(obj, "Decimate")


def mhmat_textures(mhmat):
    res = {}
    if not mhmat or not os.path.isfile(mhmat):
        return res
    for line in open(mhmat, encoding="utf-8", errors="ignore"):
        parts = line.strip().split(None, 1)
        if len(parts) == 2 and parts[0] in ("diffuseTexture", "normalmapTexture", "aomapTexture"):
            res[parts[0]] = os.path.join(os.path.dirname(mhmat), parts[1].strip())
    return res


def save_resized(src, dst_name, edit=None):
    img = bpy.data.images.load(src)
    img.scale(TEX, TEX)
    if edit is not None:
        px = np.array(img.pixels[:], dtype=np.float32).reshape(TEX, TEX, 4)
        edit(px)
        img.pixels[:] = px.ravel()
    img.filepath_raw = os.path.join(OUT, dst_name)
    img.file_format = "PNG"
    img.save()
    return dst_name


def stubble_mask(body, head_top):
    """Máscara UV (TEX x TEX) de barba de tres días: mandíbula, mentón y bigote, sin labios."""
    me = body.data
    co = np.array([v.co for v in me.vertices])
    # El modelo mira hacia -Y en Blender.
    chin, nose = head_top - 0.245, head_top - 0.152
    w = np.zeros(len(co))
    head_y = co[(co[:, 2] > head_top - 0.12)][:, 1].mean()
    for i, (x, y, z) in enumerate(co):
        if z < chin or z > nose or y > head_y + 0.04:
            continue
        # Atenuación suave arriba (pómulos), abajo (cuello) y hacia las orejas.
        top = min(1.0, (nose - z) / 0.02)
        bot = min(1.0, (z - chin) / 0.025)
        side = min(1.0, (head_y + 0.04 - y) / 0.035)
        w[i] = max(0.0, top * bot * side)
    # Labios: centro frontal a la altura de la boca.
    front = co[:, 1].min()
    lips = (np.abs(co[:, 0]) < 0.03) & (co[:, 2] > head_top - 0.2) & (co[:, 2] < head_top - 0.172) & (co[:, 1] < front + 0.018)
    w[lips] = 0.0
    return uv_mask(me, w)


def uv_mask(me, w):
    """Rasteriza pesos por vértice al espacio UV (TEX x TEX) y los desenfoca."""
    mask = np.zeros((TEX, TEX), dtype=np.float32)
    uv = me.uv_layers.active.data
    for poly in me.polygons:
        idx = list(poly.loop_indices)
        vw = [w[me.loops[l].vertex_index] for l in idx]
        if max(vw) <= 0.0:
            continue
        uvs = [np.array(uv[l].uv) * (TEX - 1) for l in idx]
        for k in range(1, len(idx) - 1):
            tri = [0, k, k + 1]
            a, b, c = uvs[tri[0]], uvs[tri[1]], uvs[tri[2]]
            wa, wb, wc = vw[tri[0]], vw[tri[1]], vw[tri[2]]
            x0, x1 = int(min(a[0], b[0], c[0])), int(max(a[0], b[0], c[0])) + 1
            y0, y1 = int(min(a[1], b[1], c[1])), int(max(a[1], b[1], c[1])) + 1
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
            d = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
            if abs(d) < 1e-9:
                continue
            l1 = ((b[1] - c[1]) * (gx - c[0]) + (c[0] - b[0]) * (gy - c[1])) / d
            l2 = ((c[1] - a[1]) * (gx - c[0]) + (a[0] - c[0]) * (gy - c[1])) / d
            l3 = 1 - l1 - l2
            ins = (l1 >= -0.01) & (l2 >= -0.01) & (l3 >= -0.01)
            val = l1 * wa + l2 * wb + l3 * wc
            gx, gy, val = gx[ins], gy[ins], val[ins]
            ok = (gx >= 0) & (gx < TEX) & (gy >= 0) & (gy < TEX)
            mask[gy[ok], gx[ok]] = np.maximum(mask[gy[ok], gx[ok]], val[ok])
    # Desenfoque para que el borde no se note.
    return blur(mask, 4)


def blur(img, k, passes=3):
    """Desenfoque de caja (varias pasadas, aproxima un gaussiano)."""
    n = 2 * k + 1
    for _ in range(passes):
        p = np.pad(img, k, mode="edge")
        c = np.pad(np.cumsum(np.cumsum(p, 0), 1), ((1, 0), (1, 0)))
        img = (c[n:, n:] - c[:-n, n:] - c[n:, :-n] + c[:-n, :-n]) / (n * n)
    return img


# Barro: bajo de los jeans y suela/puntera de las botas (altura sobre el suelo, en metros).
MUD = {"male_casualsuit05": (0.34, 0.22), "toigo_ankle_boots_male": (0.1, 0.07)}
MUD_COLOR = (0.16, 0.12, 0.085)


def mud_edit(obj, top, fade, seed):
    co = np.array([v.co for v in obj.data.vertices])
    w = np.clip((top - co[:, 2]) / fade, 0.0, 1.0)
    mask = uv_mask(obj.data, w)
    rng = np.random.default_rng(seed)
    # Manchas: ruido grueso (escalado) por el borde, para que no sea una franja pareja.
    blot = blur(np.kron(rng.random((TEX // 16, TEX // 16)), np.ones((16, 16))), 8).astype(np.float32)
    blot = np.clip((blot - blot.mean()) * 4.0 + 0.5, 0.0, 1.0)

    def edit(px):
        m = np.clip(mask * 1.4 - (1.0 - blot) * 0.5, 0.0, 1.0) * 0.8
        for ch in range(3):
            px[:, :, ch] = px[:, :, ch] * (1.0 - m) + MUD_COLOR[ch] * m
    return edit


def main():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    body = HumanService.create_human(macro_detail_dict=MACRO)
    TargetService.bake_targets(body)
    rig = HumanService.add_builtin_rig(body, "game_engine")
    rig.name = rig.data.name = "Father_MH"
    body.name = "Body"

    pieces = []
    for subdir, rel, budget in PIECES:
        path = AssetService.find_asset_absolute_path(rel, asset_subdir=subdir)
        if path is None:
            raise IOError("Falta el asset " + rel)
        asset_type = subdir if subdir != "clothes" else "Clothes"
        obj = HumanService.add_mhclo_asset(path, body, asset_type=asset_type, subdiv_levels=0, material_type="MAKESKIN")
        name = os.path.splitext(os.path.basename(rel))[0]
        obj.name = name
        mhmat = None
        folder = os.path.dirname(path)
        for f in os.listdir(folder):
            if f.endswith(".mhmat"):
                mhmat = os.path.join(folder, f)
        if subdir == "eyes":
            mhmat = AssetService.find_asset_absolute_path("materials/brown.mhmat", asset_subdir="eyes")
        pieces.append((obj, budget, mhmat))

    # Partes del cuerpo cubiertas por la ropa: aplicar las máscaras (grupo "body" y grupos de borrado).
    bpy.context.view_layer.objects.active = body
    for m in list(body.modifiers):
        if m.type == "MASK":
            apply_modifier(body, m.name)
    head_top = max(v.co.z for v in body.data.vertices)
    for o in [body] + [p[0] for p in pieces]:
        for m in list(o.modifiers):
            if m.type not in ("ARMATURE", "MASK"):
                o.modifiers.remove(m)

    report = {"pieces": {}, "triangles": 0}
    # Piel con barba de tres días pintada sobre el difuso.
    skin_tex = mhmat_textures(AssetService.find_asset_absolute_path(SKIN.split("/", 1)[1], asset_subdir="skins"))
    mask = stubble_mask(body, head_top)
    rng = np.random.default_rng(38)
    grain = rng.random((TEX, TEX)).astype(np.float32)

    def stubble(px):
        m = mask * (0.72 + 0.28 * grain)
        for ch in range(3):
            px[:, :, ch] *= 1.0 - 0.5 * m
        px[:, :, 0] *= 1.0 - 0.06 * m

    decimate(body, BODY_BUDGET)
    report["pieces"]["Body"] = {"tris": tri_count(body), "diffuse": save_resized(skin_tex["diffuseTexture"], "Father_Skin_D.png", stubble)}
    for obj, budget, mhmat in pieces:
        decimate(obj, budget)
        info = {"tris": tri_count(obj)}
        for key, tex in mhmat_textures(mhmat).items():
            short = {"diffuseTexture": "D", "normalmapTexture": "N", "aomapTexture": "AO"}[key]
            edit = None
            if key == "diffuseTexture" and obj.name in MUD:
                edit = mud_edit(obj, MUD[obj.name][0], MUD[obj.name][1], len(obj.name))
            info[key.replace("Texture", "").replace("map", "")] = save_resized(tex, "Father_%s_%s.png" % (obj.name, short), edit)
        report["pieces"][obj.name] = info
    report["triangles"] = sum(p["tris"] for p in report["pieces"].values())

    # Una sola malla con submallas: un SkinnedMeshRenderer, menos draw calls en iPhone.
    bpy.ops.object.select_all(action="DESELECT")
    objs = [body] + [p[0] for p in pieces]
    for o in objs:
        # Un material por pieza, con el nombre de la pieza (Unity lo usa para asignar el suyo).
        o.data.materials.clear()
        mat = bpy.data.materials.get(o.name) or bpy.data.materials.new(o.name)
        o.data.materials.append(mat)
        o.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    body.name = "Father_Body"

    rig.select_set(True)
    body.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, "Father_MH.fbx"), use_selection=True, object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False, bake_anim=False, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", use_armature_deform_only=True, path_mode="STRIP")
    report["bones"] = [b.name for b in rig.data.bones]
    report["height"] = head_top
    with open(os.path.join(OUT, "father_mh.json"), "w") as f:
        json.dump(report, f, indent=2)
    print("FATHER_MH: OK", json.dumps({k: v["tris"] for k, v in report["pieces"].items()}), "total", report["triangles"])


main()
