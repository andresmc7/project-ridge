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
        public static string Capture(string file, Vector3 focus, int width = 1600, int height = 900, bool includeUI = false)
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
            // Con includeUI, el HUD (overlay) pasa un momento a "screen space camera" para salir en la captura.
            var canvases = new System.Collections.Generic.List<(Canvas c, RenderMode mode)>();
            if (includeUI)
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        canvases.Add((c, c.renderMode));
                        c.renderMode = RenderMode.ScreenSpaceCamera;
                        c.worldCamera = cam;
                        c.planeDistance = cam.nearClipPlane + 0.05f;
                        Canvas.ForceUpdateCanvases();
                    }
            cam.Render();
            foreach (var (c, mode) in canvases) c.renderMode = mode;
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

        /// <summary>Vista cenital ortográfica (QA de formas: los escondites no deben verse como círculos).</summary>
        public static string TopDown(string file, Vector3 center, float halfWidth, int width = 1600, int height = 900)
        {
            var camGo = new GameObject("TopDownCam");
            var cam = camGo.AddComponent<Camera>();
            cam.CopyFrom(Camera.main);
            cam.orthographic = true;
            cam.orthographicSize = halfWidth * height / width;
            cam.transform.SetPositionAndRotation(center + Vector3.up * 60f, Quaternion.Euler(90f, 0f, 0f));
            cam.farClipPlane = 200f;
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
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
            Object.DestroyImmediate(camGo);
            return file;
        }

        /// <summary>
        /// Histograma de una captura (POLISH_PASS 3.10): fracción de píxeles con luminancia &lt; 0.08 (sombra profunda) y
        /// &gt; 0.6 (islas de luz), y la media. Luminancia Rec.709 sobre los valores del PNG (sRGB 0–1).
        /// </summary>
        public static (float dark, float bright, float mean) Histogram(string file)
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(file));
            var px = tex.GetPixels32();
            int dark = 0, bright = 0;
            double sum = 0;
            foreach (var c in px)
            {
                float l = (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
                if (l < 0.08f) dark++;
                if (l > 0.6f) bright++;
                sum += l;
            }
            Object.DestroyImmediate(tex);
            return (dark / (float)px.Length, bright / (float)px.Length, (float)(sum / px.Length));
        }

        /// <summary>Dos imágenes lado a lado a la misma altura (comparación con la imagen objetivo).</summary>
        public static string SideBySide(string left, string right, string outFile, int height = 720)
        {
            Texture2D Load(string f) { var t = new Texture2D(2, 2); t.LoadImage(File.ReadAllBytes(f)); return t; }
            var a = Load(left);
            var b = Load(right);
            int wa = Mathf.RoundToInt(a.width * height / (float)a.height), wb = Mathf.RoundToInt(b.width * height / (float)b.height);
            var outTex = new Texture2D(wa + wb + 8, height, TextureFormat.RGB24, false);
            var fill = new Color32[outTex.width * height];
            outTex.SetPixels32(fill);
            void Blit(Texture2D src, int x0, int w)
            {
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < w; x++)
                        outTex.SetPixel(x0 + x, y, src.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / height));
            }
            Blit(a, 0, wa);
            Blit(b, wa + 8, wb);
            outTex.Apply();
            File.WriteAllBytes(outFile, outTex.EncodeToPNG());
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Object.DestroyImmediate(outTex);
            return outFile;
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
            foreach (var smr in copy.GetComponentsInChildren<SkinnedMeshRenderer>()) { smr.forceMatrixRecalculationPerRender = true; smr.updateWhenOffscreen = true; }
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
            // La primera evaluación del grafo no llega a los huesos: se descarta para que la celda 0 no salga en pose de enlace.
            graph.Evaluate();

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
