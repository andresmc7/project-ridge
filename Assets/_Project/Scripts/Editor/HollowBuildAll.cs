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
            // Primer avistamiento: al entrar en la sala 1, un Lurker cruza el borde del acantilado norte y se detiene a mirar.
            log.AppendLine(CharacterBuilder.BuildApparition(OnGround(tr, -47.5f, 0f), new[] { OnGround(tr, -29f, 8.6f), OnGround(tr, -36f, 8.3f), OnGround(tr, -43f, 8.7f), OnGround(tr, -46.5f, 14f) }));
            log.AppendLine(ItemsSetupBuilder.BuildHandFlare());
            log.AppendLine(SetupFeel());
            log.AppendLine(ControlsBuilder.Setup());
            log.Append(BuildAtmosphere(UnityEngine.Terrain.activeTerrain, UnityEngine.GameObject.Find("Player")));
            log.AppendLine(HudBuilder.Build());
            log.AppendLine(HudBuilder.BuildFlow());
            log.AppendLine(CaptureBuilder.Setup());
            if (AssetDatabase.LoadAssetAtPath<ProjectRidge.Core.AudioBank>("Assets/_Project/Data/AudioBank.asset") == null)
                log.AppendLine(SoundSynth.Build());
            log.AppendLine(KenneyAudio.Assign());
            log.AppendLine(SoundSynth.SetupScene());
            return log.ToString();
        }
    }
}
