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
            var tr = UnityEngine.Terrain.activeTerrain;
            // Primer avistamiento: al pisar el sendero, un Lurker cruza la otra orilla entre la niebla.
            log.AppendLine(CharacterBuilder.BuildApparition(OnGround(tr, -14.5f, -3.5f), new[] { OnGround(tr, -2f, 13f), OnGround(tr, -2.5f, 4f), OnGround(tr, -2.5f, -4.5f), OnGround(tr, -1.5f, -15f) }));
            log.Append(BuildAtmosphere(UnityEngine.Terrain.activeTerrain, UnityEngine.GameObject.Find("Player")));
            log.AppendLine(HudBuilder.Build());
            log.AppendLine(HudBuilder.BuildFlow());
            if (AssetDatabase.LoadAssetAtPath<ProjectRidge.Core.AudioBank>("Assets/_Project/Data/AudioBank.asset") == null)
                log.AppendLine(SoundSynth.Build());
            log.AppendLine(SoundSynth.SetupScene());
            return log.ToString();
        }
    }
}
