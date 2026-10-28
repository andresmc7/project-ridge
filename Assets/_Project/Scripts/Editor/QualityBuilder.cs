using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Niveles de calidad: "Mobile" (iPhone/iPad, Mobile_RPAsset) y "PC" (Windows, PC_RPAsset). El nivel Mobile se
    /// perdió el 2026-10-20 al reserializarse QualitySettings y el iPhone quedó sin ningún nivel válido (PC lo excluye):
    /// Build All lo vuelve a crear si falta.
    /// </summary>
    public static class QualityBuilder
    {
        const string MobileAsset = "Assets/Settings/Mobile_RPAsset.asset";

        public static string EnsureLevels()
        {
            var qs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var so = new SerializedObject(qs);
            var levels = so.FindProperty("m_QualitySettings");
            int mobile = -1, pc = -1;
            for (int i = 0; i < levels.arraySize; i++)
            {
                string n = levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                if (n == "Mobile") mobile = i;
                if (n == "PC") pc = i;
            }
            if (pc < 0) return "quality: no PC level (not touching it)";

            bool created = false;
            if (mobile < 0)
            {
                // Copia de PC con los valores que tenía Mobile antes de perderse (QualitySettings de 1ba7a37^).
                levels.InsertArrayElementAtIndex(pc);
                mobile = pc;
                pc++;
                created = true;
            }
            var m = levels.GetArrayElementAtIndex(mobile);
            m.FindPropertyRelative("name").stringValue = "Mobile";
            m.FindPropertyRelative("customRenderPipeline").objectReferenceValue = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(MobileAsset);
            m.FindPropertyRelative("anisotropicTextures").intValue = 1;
            m.FindPropertyRelative("lodBias").floatValue = 1f;
            m.FindPropertyRelative("shadowmaskMode").intValue = 0;
            m.FindPropertyRelative("skinWeights").intValue = 2;
            SetPlatforms(m.FindPropertyRelative("excludedTargetPlatforms"), "Standalone");
            SetPlatforms(levels.GetArrayElementAtIndex(pc).FindPropertyRelative("excludedTargetPlatforms"), "Android", "iPhone");

            // Calidad por defecto de cada plataforma.
            var map = so.FindProperty("m_PerPlatformDefaultQuality");
            void Default(string platform, int level)
            {
                for (int i = 0; i < map.arraySize; i++)
                {
                    var e = map.GetArrayElementAtIndex(i);
                    if (e.FindPropertyRelative("first").stringValue == platform) { e.FindPropertyRelative("second").intValue = level; return; }
                }
                map.InsertArrayElementAtIndex(map.arraySize);
                var added = map.GetArrayElementAtIndex(map.arraySize - 1);
                added.FindPropertyRelative("first").stringValue = platform;
                added.FindPropertyRelative("second").intValue = level;
            }
            Default("iPhone", mobile);
            Default("Android", mobile);
            Default("Standalone", pc);
            so.FindProperty("m_CurrentQuality").intValue = pc; // el editor (Windows) sigue en PC
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return "quality: " + (created ? "Mobile level restored" : "Mobile level ok") + " (iPhone → " + QualitySettings.names[mobile] + ", Standalone → " + QualitySettings.names[pc] + ")";
        }

        static void SetPlatforms(SerializedProperty list, params string[] platforms)
        {
            list.arraySize = platforms.Length;
            for (int i = 0; i < platforms.Length; i++) list.GetArrayElementAtIndex(i).stringValue = platforms[i];
        }
    }
}
