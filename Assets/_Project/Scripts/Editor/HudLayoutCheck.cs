using System.Collections.Generic;
using System.IO;
using ProjectRidge.UI;
using UnityEditor;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// QA del HUD en los equipos objetivo (CLAUDE.md: iPhone 16–18 y iPad con chip M, solo horizontal). En el editor
    /// Screen.safeArea es la pantalla entera: aquí se simula el área segura de cada equipo, se renderiza a su resolución
    /// y se comprueba que los controles quedan dentro del área segura, no se tapan entre sí y miden al menos 44 pt
    /// (mínimo de toque de Apple). Deja una captura por equipo en docs/progress.
    /// </summary>
    public static class HudLayoutCheck
    {
        struct Device
        {
            public string name; public int w, h; public float scale; // px por punto
            public float left, right, top, bottom;                    // márgenes del área segura en horizontal (pt)
            public Device(string n, int w, int h, float s, float l, float r, float t, float b) { name = n; this.w = w; this.h = h; scale = s; left = l; right = r; top = t; bottom = b; }
        }

        // Resoluciones nativas en horizontal. Márgenes de Apple para horizontal: iPhone con Dynamic Island 59–62 pt a los
        // lados y 21 pt abajo (indicador de inicio); iPad sin botón 24 pt arriba (barra de estado) y 20 pt abajo.
        static readonly Device[] Devices =
        {
            new Device("iPhone16", 2556, 1179, 3f, 59f, 59f, 0f, 21f),
            new Device("iPhone16ProMax", 2868, 1320, 3f, 62f, 62f, 0f, 21f),
            new Device("iPadPro11", 2420, 1668, 2f, 0f, 0f, 24f, 20f),
            new Device("iPadAir13", 2732, 2048, 2f, 0f, 0f, 24f, 20f),
        };

        static readonly string[] Buttons = { "Action", "Object", "Flashlight", "Pause", "HealHold" };
        static readonly string[] Labels = { "Objective", "SurvivalHud/Prompt", "SurvivalHud/Toast", "SurvivalHud/Pickup" };
        const float MinTouchPt = 44f;

        static void RefreshScaler(GameObject hud)
        {
            var cs = hud.GetComponent<UnityEngine.UI.CanvasScaler>();
            cs.enabled = false;
            cs.enabled = true;
            Canvas.ForceUpdateCanvases();
        }

        [MenuItem("Project Ridge/QA/HUD layout on iPhone and iPad")]
        public static string Run()
        {
            var hud = GameObject.Find("HUD");
            var canvas = hud.GetComponent<Canvas>();
            var safe = (RectTransform)hud.transform.Find("SafeArea");
            var cam = Camera.main;
            var report = new System.Text.StringBuilder();
            int fails = 0;

            // Estado de juego para la captura: HUD visible, sin título ni menús.
            var groups = new List<(CanvasGroup g, float a)>();
            void Set(string path, float alpha)
            {
                var t = hud.transform.Find(path);
                if (t == null || !t.TryGetComponent(out CanvasGroup g)) return;
                groups.Add((g, g.alpha));
                g.alpha = alpha;
            }
            Set("Flow/TitlePanel", 0f);
            Set("Flow/PausePanel", 0f);
            Set("Flow/CreditsPanel", 0f);
            Set("Flow/EndPanel", 0f);
            Set("Flow/Fade", 0f);

            var mode = canvas.renderMode;
            var prevTarget = cam.targetTexture;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = cam.nearClipPlane + 0.05f;
                foreach (var d in Devices)
                {
                    var safeNorm = Rect.MinMaxRect(d.left * d.scale / d.w, d.bottom * d.scale / d.h, 1f - d.right * d.scale / d.w, 1f - d.top * d.scale / d.h);
                    SafeAreaFitter.Simulated = safeNorm;
                    SafeAreaFitter.Fit(safe, safeNorm);
                    var rt = RenderTexture.GetTemporary(d.w, d.h, 24, RenderTextureFormat.ARGB32);
                    cam.targetTexture = rt;
                    // Escala del equipo, con la textura ya asignada (el canvas toma su tamaño de la cámara). El CanvasScaler
                    // la aplica en su próxima actualización: forzarla ya.
                    HudScaler.SimulatedPixelsPerPoint = d.scale;
                    hud.GetComponent<HudScaler>().Apply();
                    RefreshScaler(hud);
                    Canvas.ForceUpdateCanvases();
                    var safePx = new Rect(safeNorm.x * d.w, safeNorm.y * d.h, safeNorm.width * d.w, safeNorm.height * d.h);

                    Rect PixelRect(RectTransform r)
                    {
                        var c = new Vector3[4];
                        r.GetWorldCorners(c);
                        Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, c[0]), b = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
                        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                    }
                    bool Inside(Rect r) => r.xMin >= safePx.xMin - 1f && r.yMin >= safePx.yMin - 1f && r.xMax <= safePx.xMax + 1f && r.yMax <= safePx.yMax + 1f;

                    var line = new System.Text.StringBuilder(d.name + " " + d.w + "x" + d.h + ":");
                    var rects = new List<(string n, Rect r)>();
                    foreach (var b in Buttons)
                    {
                        var t = safe.Find("TouchControls/" + b) as RectTransform;
                        if (t == null) { line.Append(" " + b + " missing;"); fails++; continue; }
                        var r = PixelRect(t);
                        rects.Add((b, r));
                        float pt = Mathf.Min(r.width, r.height) / d.scale;
                        if (!Inside(r)) { line.Append(" " + b + " OUTSIDE safe area;"); fails++; }
                        if (pt < MinTouchPt - 0.5f) { line.Append(" " + b + " " + pt.ToString("0") + " pt (< 44);"); fails++; }
                        else line.Append(" " + b + " " + pt.ToString("0") + "pt");
                    }
                    for (int i = 0; i < rects.Count; i++)
                        for (int j = i + 1; j < rects.Count; j++)
                            if (rects[i].r.Overlaps(rects[j].r)) { line.Append(" OVERLAP " + rects[i].n + "/" + rects[j].n + ";"); fails++; }
                    foreach (var l in Labels)
                    {
                        var t = safe.Find(l) as RectTransform;
                        if (t == null) continue;
                        var r = PixelRect(t);
                        if (!Inside(r)) { line.Append(" label " + l + " OUTSIDE safe area;"); fails++; }
                        foreach (var (n, br) in rects)
                            if (r.Overlaps(br)) { line.Append(" label " + l + " over " + n + ";"); fails++; }
                    }
                    // Menús (fuera del área segura del HUD, pero deben caber en la pantalla y fuera de las esquinas).
                    foreach (var menu in new[] { "Flow/PausePanel", "Flow/CreditsPanel", "Flow/EndPanel/Buttons" })
                    {
                        var m = hud.transform.Find(menu);
                        if (m == null) continue;
                        foreach (var sel in m.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
                        {
                            var r = PixelRect((RectTransform)sel.transform);
                            if (!Inside(r)) { line.Append(" " + menu + "/" + sel.name + " OUTSIDE;"); fails++; }
                        }
                    }
                    report.AppendLine(line.ToString());

                    cam.Render();
                    var active = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(d.w, d.h, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, d.w, d.h), 0, 0);
                    // Marco del área segura en rojo (para revisar a ojo).
                    for (int x = (int)safePx.xMin; x < (int)safePx.xMax; x++) { tex.SetPixel(x, (int)safePx.yMin, Color.red); tex.SetPixel(x, (int)safePx.yMax - 1, Color.red); }
                    for (int y = (int)safePx.yMin; y < (int)safePx.yMax; y++) { tex.SetPixel((int)safePx.xMin, y, Color.red); tex.SetPixel((int)safePx.xMax - 1, y, Color.red); }
                    tex.Apply();
                    RenderTexture.active = active;
                    Directory.CreateDirectory("docs/progress");
                    File.WriteAllBytes("docs/progress/hud_" + d.name + ".png", tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    cam.targetTexture = prevTarget;
                    RenderTexture.ReleaseTemporary(rt);
                }
            }
            finally
            {
                cam.targetTexture = prevTarget;
                canvas.renderMode = mode;
                HudScaler.SimulatedPixelsPerPoint = null;
                hud.GetComponent<HudScaler>().Apply();
                RefreshScaler(hud);
                SafeAreaFitter.Simulated = null;
                SafeAreaFitter.Fit(safe, new Rect(0f, 0f, 1f, 1f));
                foreach (var (g, a) in groups) g.alpha = a;
                Canvas.ForceUpdateCanvases();
            }
            string result = "HUD layout: " + (fails == 0 ? "OK" : fails + " problems") + "\n" + report;
            Debug.Log(result);
            return result;
        }
    }
}
