using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Sonidos grabados de Kenney "Impact Sounds" (CC0, ver ASSETS.md) para lo que la síntesis no logra creíble:
    /// pisadas (pasto, madera), golpes de cuerpo y escombros. Ajusta la importación (cortos, descomprimidos al
    /// cargar: sin latencia) y los asigna al AudioBank.
    /// </summary>
    public static class KenneyAudio
    {
        const string Dir = "Assets/ThirdParty/Kenney/ImpactSounds";
        const string BankPath = "Assets/_Project/Data/AudioBank.asset";

        static AudioClip[] Set(string prefix)
        {
            var list = new List<AudioClip>();
            for (int i = 0; i < 5; i++)
            {
                string path = Dir + "/" + prefix + "_00" + i + ".ogg";
                var imp = AssetImporter.GetAtPath(path) as AudioImporter;
                if (imp == null) continue;
                var s = imp.defaultSampleSettings;
                if (s.loadType != AudioClipLoadType.DecompressOnLoad)
                {
                    s.loadType = AudioClipLoadType.DecompressOnLoad;
                    s.compressionFormat = AudioCompressionFormat.Vorbis;
                    s.quality = 0.7f;
                    imp.defaultSampleSettings = s;
                    imp.forceToMono = true;
                    imp.SaveAndReimport();
                }
                list.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }
            return list.ToArray();
        }

        [MenuItem("Project Ridge/Audio/Assign Kenney Sounds")]
        public static string Assign()
        {
            var bank = AssetDatabase.LoadAssetAtPath<ProjectRidge.Core.AudioBank>(BankPath);
            if (bank == null) return "no audio bank";
            bank.stepsGrass = Set("footstep_grass");
            bank.stepsWood = Set("footstep_wood");
            bank.stepsDirt = Set("footstep_concrete");
            bank.bodyHits = Set("impactPunch_heavy");
            bank.bodyFalls = Set("impactSoft_heavy");
            var wood = new List<AudioClip>(Set("impactWood_light"));
            wood.AddRange(Set("impactPlank_medium"));
            bank.debrisWood = wood.ToArray();
            bank.debrisMetal = Set("impactMetal_light");
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            return "kenney sounds: grass " + bank.stepsGrass.Length + ", wood " + bank.stepsWood.Length + ", hits " + bank.bodyHits.Length + ", debris " + (bank.debrisWood.Length + bank.debrisMetal.Length);
        }
    }
}
