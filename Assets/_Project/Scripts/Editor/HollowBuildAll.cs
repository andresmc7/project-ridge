using UnityEditor;
using UnityEditor.SceneManagement;

namespace ProjectRidge.EditorTools
{
    public static partial class HollowBuilder
    {
        /// <summary>Reconstruye The Hollow completo en orden: nivel, personajes, HUD, flujo de juego y audio.</summary>
        [MenuItem("Project Ridge/The Hollow/Build All")]
        public static string BuildAll()
        {
            var log = new System.Text.StringBuilder();
            EditorSceneManager.SaveOpenScenes();
            log.Append(Build());
            log.Append(CharacterBuilder.Build());
            log.AppendLine(HudBuilder.Build());
            log.AppendLine(HudBuilder.BuildFlow());
            if (AssetDatabase.LoadAssetAtPath<ProjectRidge.Core.AudioBank>("Assets/_Project/Data/AudioBank.asset") == null)
                log.AppendLine(SoundSynth.Build());
            log.AppendLine(SoundSynth.SetupScene());
            return log.ToString();
        }
    }
}
