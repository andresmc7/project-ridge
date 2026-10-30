using System.Text;
using ProjectRidge.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Medida de rendimiento repetible en el editor: con el asset de URP del perfil Mobile, lleva al papá a los mismos 6 puntos
    /// del nivel y anota batches, draws, SetPass, triángulos y proyectores de sombra (media de 20 cuadros por punto).
    /// No sustituye al Profiler en un iPhone; sirve para comparar antes/después de un cambio.
    /// Uso: en play mode, PerfProbe.Run(); deja "PERF: DONE" en la consola y sale de play mode.
    /// </summary>
    public static class PerfProbe
    {
        static readonly Vector2[] Points = { new(-55f, 0f), new(-24f, 0f), new(-1.5f, 0.2f), new(24f, 0f), new(47.6f, -0.5f), new(60f, -3f) };
        const float SettleSeconds = 1.5f;
        const int SampleFrames = 20;

        const string MobileAsset = "Assets/Settings/Mobile_RPAsset.asset";

        static int point, frames;
        static UnityEngine.Rendering.RenderPipelineAsset previousPipeline;
        static double nextAt;
        static float batches, draws, setPass, tris, casters;
        static StringBuilder log;
        static PlayerController player;

        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("PERF: entra en play mode primero"); return; }
            player = Object.FindFirstObjectByType<PlayerController>();
            // El nivel Mobile está desactivado para Windows y el editor no deja activarlo: se fuerza su asset de URP.
            previousPipeline = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(MobileAsset);
            log = new StringBuilder("PERF (" + QualitySettings.renderPipeline.name + "): point | batches | draws | setpass | tris | shadow casters\n");
            point = -1;
            Next();
            EditorApplication.update += Tick;
        }

        // ------------------------------------------------------------------ Desglose por grupo

        static GameObject[] groups;
        static int group;
        static float baseBatches, baseCasters, baseTris;

        /// <summary>
        /// En el punto donde esté el papá: mide todo encendido y luego apaga un grupo del nivel por vez (hijos de
        /// "Hollow", más Atmosphere, Searchlights y los Lurker) y anota cuánto bajan batches, proyectores y triángulos.
        /// </summary>
        public static void Breakdown()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("PERF: entra en play mode primero"); return; }
            var list = new System.Collections.Generic.List<GameObject>();
            var hollow = GameObject.Find("Hollow");
            if (hollow != null) foreach (Transform c in hollow.transform) if (c.GetComponentInChildren<Renderer>() != null) list.Add(c.gameObject);
            foreach (var n in new[] { "Atmosphere", "Searchlights", "Lurker", "Lurker_B", "Lurker_C", "Lurker_D", "Items" })
            { var g = GameObject.Find(n); if (g != null) list.Add(g); }
            groups = list.ToArray();
            previousPipeline = QualitySettings.renderPipeline;
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(MobileAsset);
            player = Object.FindFirstObjectByType<PlayerController>();
            log = new StringBuilder("PERF BREAKDOWN at " + player.transform.position + " (" + QualitySettings.renderPipeline.name + "): group | −batches | −casters | −tris\n");
            group = -1;
            batches = casters = tris = 0f; frames = 0;
            nextAt = EditorApplication.timeSinceStartup + 0.5;
            EditorApplication.update += BreakdownTick;
        }

        static void BreakdownTick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= BreakdownTick; return; }
            if (EditorApplication.timeSinceStartup < nextAt) return;
            batches += UnityStats.batches; casters += UnityStats.shadowCasters; tris += UnityStats.triangles;
            if (++frames < SampleFrames) return;
            float n = frames;
            if (group < 0) { baseBatches = batches / n; baseCasters = casters / n; baseTris = tris / n; log.AppendLine("all | " + baseBatches.ToString("0") + " | " + baseCasters.ToString("0") + " | " + (baseTris / 1000f).ToString("0") + "k"); }
            else
            {
                log.AppendLine(groups[group].name + " | " + (baseBatches - batches / n).ToString("0") + " | " + (baseCasters - casters / n).ToString("0") + " | " + ((baseTris - tris / n) / 1000f).ToString("0") + "k");
                groups[group].SetActive(true);
            }
            group++;
            batches = casters = tris = 0f; frames = 0;
            if (group < groups.Length) { groups[group].SetActive(false); nextAt = EditorApplication.timeSinceStartup + 0.3; return; }
            EditorApplication.update -= BreakdownTick;
            QualitySettings.renderPipeline = previousPipeline;
            Debug.Log(log + "PERF: DONE");
        }

        static void Next()
        {
            point++;
            batches = draws = setPass = tris = casters = 0f;
            frames = 0;
            if (point >= Points.Length) return;
            var p = new Vector3(Points[point].x, 1f, Points[point].y);
            if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) p = hit.position + Vector3.up * 0.05f;
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = p;
            cc.enabled = true;
            player.CancelStandingTurn();
            nextAt = EditorApplication.timeSinceStartup + SettleSeconds;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
            if (EditorApplication.timeSinceStartup < nextAt) return;
            batches += UnityStats.batches; draws += UnityStats.drawCalls; setPass += UnityStats.setPassCalls;
            tris += UnityStats.triangles; casters += UnityStats.shadowCasters;
            if (++frames < SampleFrames) return;
            float n = frames;
            log.AppendLine(Points[point] + " | " + (batches / n).ToString("0") + " | " + (draws / n).ToString("0") + " | " + (setPass / n).ToString("0") + " | " + (tris / n / 1000f).ToString("0") + "k | " + (casters / n).ToString("0"));
            Next();
            if (point < Points.Length) return;
            EditorApplication.update -= Tick;
            QualitySettings.renderPipeline = previousPipeline;
            Debug.Log(log + "PERF: DONE");
            // Entrar en play con runInBackground lo deja en 1 en ProjectSettings; se restaura al volver al editor (como
            // PolishSuite). La marca va en SessionState porque salir de play puede recargar el dominio.
            SessionState.SetBool(RestoreKey, true);
            EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
        }

        const string RestoreKey = "ridge.perf.restoreRunInBackground";

        [InitializeOnLoadMethod]
        static void WatchPlayMode() => EditorApplication.playModeStateChanged += RestoreRunInBackground;

        static void RestoreRunInBackground(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(RestoreKey, false)) return;
            SessionState.EraseBool(RestoreKey);
            PlayerSettings.runInBackground = false;
            AssetDatabase.SaveAssets();
        }
    }
}
