# Convierte segmentos de tomas de 100STYLE (BVH) a FBX para Unity, un archivo por clip.
#
# Uso: blender -b --factory-startup --python Tools/Blender/convert_100style.py -- <carpeta 100STYLE> <carpeta salida>
#
# Los segmentos salieron de Tools/Blender/mine_100style.py (arranques, frenadas, giros en el sitio, pivote) y de
# Frame_Cuts.csv (idles), siempre dentro del rango válido de la toma (fuera, el actor está en pose T de calibración)
# salvo las frenadas, que terminan antes de esa pose. Cuadros a 60 fps de la toma original; el FBX conserva los 60 fps.

import os
import sys

import bpy

# nombre del clip -> (toma, primer cuadro, último cuadro)
SEGMENTS = {
    "S100_WalkStart": ("Neutral_FW", 5942, 6012),
    "S100_WalkStop": ("Neutral_FW", 7748, 7831),
    "S100_RunStop": ("Neutral_FR", 4231, 4311),
    "S100_TurnA": ("Neutral_FW", 406, 461),
    "S100_TurnB": ("Neutral_FW", 5919, 5971),
    "S100_Pivot": ("Neutral_FR", 409, 469),
    "S100_TenseIdle": ("BentForward_ID", 600, 1200),
    "S100_LookAround": ("Followed_ID", 640, 830),
}


def fold_chest3(arm, first, last):
    """El humanoide de Unity tiene 3 huesos de columna y 100STYLE 4: la rotación de Chest3 se suma a Chest2 (si no,
    Unity la descarta con una advertencia de importación). El error de posición es de un segmento de 9 cm."""
    pb2, pb3 = arm.pose.bones["Chest2"], arm.pose.bones["Chest3"]
    scene = bpy.context.scene
    folded = []
    for f in range(first, last + 1):
        scene.frame_set(f)
        folded.append(pb2.matrix_basis @ pb3.matrix_basis)
    for pb in (pb2, pb3):
        pb.rotation_mode = "QUATERNION"
    action = arm.animation_data.action
    for fc in list(action.fcurves):
        if fc.data_path.startswith('pose.bones["Chest2"].rotation') or fc.data_path.startswith('pose.bones["Chest3"].rotation'):
            action.fcurves.remove(fc)
    for i, f in enumerate(range(first, last + 1)):
        pb2.rotation_quaternion = folded[i].to_quaternion()
        pb3.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb2.keyframe_insert("rotation_quaternion", frame=f)
        pb3.keyframe_insert("rotation_quaternion", frame=f)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    root, out = os.path.abspath(argv[0]), os.path.abspath(argv[1])
    os.makedirs(out, exist_ok=True)
    scene = bpy.context.scene
    scene.render.fps = 60
    for clip, (take, first, last) in SEGMENTS.items():
        for o in list(bpy.data.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        for a in list(bpy.data.actions):
            bpy.data.actions.remove(a)
        path = os.path.join(root, take.split("_")[0], take + ".bvh")
        # BVH en cm: a metros. El importador pasa de Y arriba a Z arriba; la exportación FBX lo devuelve a Y arriba.
        bpy.ops.import_anim.bvh(filepath=path, global_scale=0.01, frame_start=0, use_fps_scale=False,
                                update_scene_fps=False, update_scene_duration=False, rotate_mode="NATIVE",
                                axis_forward="-Z", axis_up="Y")
        arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
        arm.name = arm.data.name = "S100"
        # Recorta la acción al segmento (cuadros de la toma = cuadros de la escena, empezando en 0).
        scene.frame_start, scene.frame_end = first, last
        fold_chest3(arm, first, last)
        bpy.ops.object.select_all(action="DESELECT")
        arm.select_set(True)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(out, clip + ".fbx"), use_selection=True, object_types={"ARMATURE"},
            add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
            bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y")
        print("S100:", clip, take, first, last, (last - first) / 60.0, "s")


main()
