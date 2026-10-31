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

        [MenuItem("Project Ridge/Claude/Enter Play (Game View)")]
        public static void EnterPlay()
        {
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }
    }
}
