using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Import settings del papá generado con MPFB2/MakeHuman (Tools/Blender/make_father.py): rig "game_engine" como
    /// Humanoid (los clips de la Universal Animation Library se reaplican igual), sin animaciones propias; los
    /// materiales del FBX solo aportan el nombre de cada pieza; texturas a 1k con normales como normal map.
    /// </summary>
    public static class MakeHumanImport
    {
        public const string Root = "Assets/ThirdParty/MakeHuman/Father";
        public const string Body = Root + "/Father_MH.fbx";

        [MenuItem("Project Ridge/Characters/Configure MakeHuman Imports")]
        public static string Configure()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(Body);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.importAnimation = false;
            imp.importBlendShapes = false;
            imp.isReadable = false;
            imp.SaveAndReimport();

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                bool normal = p.EndsWith("_N.png");
                bool ao = p.EndsWith("_AO.png");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !normal && !ao;
                ti.alphaIsTransparency = !normal && !ao;
                ti.maxTextureSize = 1024;
                ti.SaveAndReimport();
            }
            var avatar = AssetDatabase.LoadAllAssetsAtPath(Body).OfType<Avatar>().FirstOrDefault();
            return "Father_MH.fbx: avatar valid=" + (avatar != null && avatar.isValid) + " human=" + (avatar != null && avatar.isHuman);
        }

        public static Texture2D Tex(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/" + file);
    }
}
