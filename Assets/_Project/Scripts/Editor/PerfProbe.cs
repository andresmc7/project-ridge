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
            EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
        }
    }
}
