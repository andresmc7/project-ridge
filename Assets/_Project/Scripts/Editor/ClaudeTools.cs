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

        [MenuItem("Project Ridge/Claude/Enter Play (Game View)")]
        public static void EnterPlay()
        {
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }
    }
}
