using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Corre varias fases de PolishTests seguidas, cada una en su propia sesión de play mode (las pruebas suponen un
    /// nivel recién cargado). La cola y los resultados viven en SessionState: sobreviven la recarga de dominio al
    /// entrar en play. Al terminar deja "POLISH SUITE: DONE" con el resumen de cada fase en la consola.
    /// Uso: PolishSuite.Run("P0,P1,P4").
    /// </summary>
    [InitializeOnLoad]
    public static class PolishSuite
    {
        const string QueueKey = "ridge.suite.queue", ResultsKey = "ridge.suite.results";

        static double enterAt = -1;

        static PolishSuite()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Pump;
        }

        /// <summary>Vuelve a entrar en play un segundo después de salir (pedirlo en el mismo aviso de salida se ignora).</summary>
        static void Pump()
        {
            if (enterAt < 0 || EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < enterAt) return;
            enterAt = -1;
            EnterPlay();
        }

        public static bool Active => !string.IsNullOrEmpty(SessionState.GetString(QueueKey, ""));

        [MenuItem("Project Ridge/QA/Run all polish tests")]
        public static void RunAll() => Run("P0,P1,P2,P3,P4,P5,P6,P7,P8,SNEAK");

        public static void Run(string phases)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("POLISH SUITE: sal de play mode primero"); return; }
            SessionState.SetString(QueueKey, phases);
            SessionState.SetString(ResultsKey, "");
            EnterPlay();
        }

        /// <summary>Lo llama PolishTests al terminar una fase.</summary>
        public static void Report(string phase, int passed, int total, string details)
        {
            if (!Active) return;
            SessionState.SetString(ResultsKey, SessionState.GetString(ResultsKey, "") + phase + " " + passed + "/" + total + (passed < total ? "\n" + details : "") + "\n");
        }

        static void EnterPlay()
        {
            Application.runInBackground = true;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (!Active) return;
            string[] queue = SessionState.GetString(QueueKey, "").Split(',');
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                // Un cuadro para que GameFlow y la escena terminen de despertar.
                EditorApplication.delayCall += () => { if (EditorApplication.isPlaying) PolishTests.Run(queue[0].Trim()); };
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                string rest = queue.Length > 1 ? string.Join(",", queue, 1, queue.Length - 1) : "";
                SessionState.SetString(QueueKey, rest);
                if (rest.Length > 0) { enterAt = EditorApplication.timeSinceStartup + 1.0; return; }
                PlayerSettings.runInBackground = false; // el arnés lo deja en 1 en el disco (ver memoria del proyecto)
                AssetDatabase.SaveAssets();
                Debug.Log("POLISH SUITE: DONE\n" + SessionState.GetString(ResultsKey, ""));
            }
        }
    }
}
