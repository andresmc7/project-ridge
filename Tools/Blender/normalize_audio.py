# Copias normalizadas de los clips que necesitan SUBIR de volumen para llegar a su objetivo de loudness
# (POLISH_PASS 3.11: música −20, efectos −16, ambiente −24 LUFS). En el juego solo se puede bajar volumen
# (AudioSource.volume tope 1), así que esas copias se generan aquí, con el pico limitado a −1 dBFS.
#
# Uso: blender -b --factory-startup --python Tools/Blender/normalize_audio.py -- Tools/Blender/normalize_jobs.json
# La lista la escribe MusicBuilder.Assign (Unity) con: src, dst, gain (lineal), channels, rate.
# Después, en Unity, volver a correr Build All: el banco pasa a usar las copias (*_n.ogg).

import json
import os
import sys

import bpy

jobs_path = sys.argv[sys.argv.index("--") + 1]
with open(jobs_path, encoding="utf-8") as f:
    jobs = json.load(f)

scene = bpy.context.scene
scene.render.fps = 100
scene.sequence_editor_create()
done = 0
for job in jobs:
    for s in list(scene.sequence_editor.sequences):
        scene.sequence_editor.sequences.remove(s)
    strip = scene.sequence_editor.sequences.new_sound("s", job["src"], 1, 1)
    strip.volume = job["gain"]
    scene.frame_start, scene.frame_end = 1, strip.frame_final_end
    scene.render.ffmpeg.audio_channels = "MONO" if job["channels"] == 1 else "STEREO"
    scene.render.ffmpeg.audio_mixrate = int(job["rate"])
    os.makedirs(os.path.dirname(job["dst"]), exist_ok=True)
    bpy.ops.sound.mixdown(filepath=job["dst"], container="OGG", codec="VORBIS", bitrate=192)
    done += 1
    print("NORM:", os.path.basename(job["dst"]), "gain %.2f" % job["gain"])
print("NORM DONE", done)
