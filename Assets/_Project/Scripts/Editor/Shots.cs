using System.IO;
using ProjectRidge.CameraRig;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// QA visual: renderiza la cámara principal con el encuadre de juego (IsoCameraConfig) mirando a un punto y
    /// guarda un PNG. Sirve en edición y en play mode; restaura la cámara al terminar.
    /// </summary>
    public static class Shots
    {
        public static string Capture(string file, Vector3 focus, int width = 1600, int height = 900)
        {
            var cam = Camera.main;
            var config = AssetDatabase.LoadAssetAtPath<IsoCameraConfig>("Assets/_Project/Data/IsoCameraConfig.asset");
            var t = cam.transform;
            Vector3 pos = t.position;
            Quaternion rot = t.rotation;
            float fov = cam.fieldOfView;

            Quaternion r = Quaternion.Euler(config.pitch, config.yaw, 0f);
            t.SetPositionAndRotation(focus + Vector3.up * config.targetHeight + r * new Vector3(0f, 0f, -config.distance) + Quaternion.Euler(0f, config.yaw, 0f) * Vector3.forward * config.framingOffset, r);
            cam.fieldOfView = config.fieldOfView;

            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            t.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
            return file;
        }

        /// <summary>
        /// Hoja de poses: muestrea un clip sobre un cuerpo (con su Animator) en varios instantes y los pone en fila,
        /// vistos de lado a 3.2 m. Para revisar clips sin entrar en play mode.
        /// </summary>
        public static string PoseSheet(string file, GameObject body, AnimationClip clip, float[] times, float yaw = 90f, int cell = 360)
        {
            // Copia aislada lejos del nivel, con cámara y luz propias (sin post-proceso ni niebla de por medio).
            var copy = Object.Instantiate(body);
            copy.transform.SetPositionAndRotation(new Vector3(0f, 400f, 0f), Quaternion.identity);
            var anim = copy.GetComponent<Animator>();
            foreach (var mb in copy.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
            foreach (var smr in copy.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.forceMatrixRecalculationPerRender = true;
            var camGo = new GameObject("PoseCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.32f, 0.34f, 0.37f);
            cam.fieldOfView = 40f;
            var lightGo = new GameObject("PoseLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2.2f;
            lightGo.transform.rotation = Quaternion.Euler(35f, yaw + 150f, 0f);
            Vector3 center = copy.transform.position + Vector3.up * 0.9f;
            Quaternion r = Quaternion.Euler(10f, yaw + 180f, 0f);
            camGo.transform.SetPositionAndRotation(center - r * Vector3.forward * 3.4f, r);

            var graph = UnityEngine.Playables.PlayableGraph.Create("PoseSheet");
            var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "out", anim);
            var playable = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(true);
            output.SetSourcePlayable(playable);

            var sheet = new Texture2D(cell * times.Length, cell, TextureFormat.RGB24, false);
            var rt = RenderTexture.GetTemporary(cell, cell, 24, RenderTextureFormat.ARGB32);
            var active = RenderTexture.active;
            cam.targetTexture = rt;
            for (int i = 0; i < times.Length; i++)
            {
                playable.SetTime(times[i]);
                graph.Evaluate();
                cam.Render();
                RenderTexture.active = rt;
                sheet.ReadPixels(new Rect(0, 0, cell, cell), cell * i, 0);
            }
            graph.Destroy();
            sheet.Apply();
            cam.targetTexture = null;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllBytes(file, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(copy);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);
            return file;
        }
    }
}
