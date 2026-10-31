using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>Palancas de edición remota (sesión de dirección de arte, 2026-10-31).</summary>
    public static class ClaudeTools
    {
        [MenuItem("Project Ridge/Claude/Reload Active Scene From Disk")]
        public static void ReloadActiveScene()
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            var scene = EditorSceneManager.GetActiveScene();
            string path = scene.path;
            if (string.IsNullOrEmpty(path)) { Debug.LogWarning("CLAUDE: la escena activa no tiene ruta"); return; }
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log("CLAUDE: escena recargada desde disco: " + path);
        }


        /// <summary>Luz de relleno tenue que acompaña al papá: siempre legible en la sombra, sin romper el contraste.</summary>
        [MenuItem("Project Ridge/Claude/Apply Player Fill Light")]
        public static void ApplyPlayerFillLight()
        {
            var player = GameObject.Find("Player");
            if (player == null) { Debug.LogWarning("CLAUDE: no hay Player en la escena"); return; }
            var t = player.transform.Find("PlayerFill");
            GameObject go = t != null ? t.gameObject : new GameObject("PlayerFill");
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0.3f, 1.7f, -0.45f);
            var l = go.GetComponent<Light>(); if (l == null) l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 2.2f;
            l.intensity = 1.4f;
            l.useColorTemperature = true; l.colorTemperature = 3400f;
            l.color = new Color(1f, 0.93f, 0.85f);
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
            EditorSceneManager.MarkSceneDirty(player.scene);
            if (!EditorApplication.isPlaying) EditorSceneManager.SaveScene(player.scene);
            Debug.Log("CLAUDE: PlayerFill aplicada" + (EditorApplication.isPlaying ? " (solo en play; repetir fuera de play para guardar)" : " y escena guardada"));
        }


        /// <summary>Recoloca los troncos de cobertura de la escena según HollowBuilder.CoverLogs (sin reconstruir el nivel).</summary>
        [MenuItem("Project Ridge/Claude/Reposition Cover Logs")]
        public static void RepositionCoverLogs()
        {
            var terrain = Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) { Debug.LogWarning("CLAUDE: sin terreno"); return; }
            var logs = new System.Collections.Generic.List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name == "FallenLog") logs.Add(t);
            int moved = 0;
            var used = new System.Collections.Generic.HashSet<Transform>();
            foreach (var l in HollowBuilder.CoverLogs)
            {
                var target = HollowBuilder.OnGround(terrain, l.x, l.y, 0.32f);
                Transform best = null; float bd = float.MaxValue;
                foreach (var t in logs)
                {
                    if (used.Contains(t)) continue;
                    float d = Vector2.Distance(new Vector2(t.position.x, t.position.z), new Vector2(l.x, l.y));
                    if (d < bd) { bd = d; best = t; }
                }
                if (best == null || bd > 6f) continue;
                used.Add(best);
                best.SetPositionAndRotation(target, Quaternion.Euler(0f, l.z, 0f));
                moved++;
            }
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorApplication.isPlaying) EditorSceneManager.SaveScene(scene);
            Debug.Log("CLAUDE: troncos recolocados: " + moved + "/" + logs.Count);
        }


        /// <summary>Pone al papá (y PlayerSpawn) frente al hueco de la puerta este del refugio, mirando al este.</summary>
        [MenuItem("Project Ridge/Claude/Spawn Player At Shelter Door")]
        public static void SpawnAtShelterDoor()
        {
            var wall = GameObject.Find("RangerShelter/Wall_E");
            var player = GameObject.Find("Player");
            if (wall == null || player == null) { Debug.LogWarning("CLAUDE: falta Wall_E o Player"); return; }
            var rends = wall.GetComponentsInChildren<Renderer>();
            if (rends.Length < 2) { Debug.LogWarning("CLAUDE: Wall_E sin dos paneles"); return; }
            System.Array.Sort(rends, (a, b) => a.bounds.center.z.CompareTo(b.bounds.center.z));
            // hueco entre el panel más al sur y el siguiente
            float gapMin = rends[0].bounds.max.z, gapMax = rends[1].bounds.min.z;
            float gapZ = (gapMin + gapMax) * 0.5f;
            float wallX = rends[0].bounds.center.x;
            var terrain = Object.FindFirstObjectByType<Terrain>();
            var start = HollowBuilder.OnGround(terrain, wallX - 1.4f, gapZ, 0.25f);
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(start, Quaternion.Euler(0f, 90f, 0f));
            if (cc != null) cc.enabled = true;
            var spawn = GameObject.Find("PlayerSpawn");
            if (spawn != null) spawn.transform.SetPositionAndRotation(start, player.transform.rotation);
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorApplication.isPlaying) EditorSceneManager.SaveScene(scene);
            Debug.Log($"CLAUDE: spawn en la puerta: x={start.x:F2} z={gapZ:F2} (hueco {gapMin:F2}..{gapMax:F2})");
        }

        [MenuItem("Project Ridge/Claude/Enter Play (Game View)")]
        public static void EnterPlay()
        {
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }
    }
}
