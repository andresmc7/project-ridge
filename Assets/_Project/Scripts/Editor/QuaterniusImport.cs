using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Configura los FBX de Quaternius (ThirdParty, sin modificar los archivos; solo import settings):
    /// rig Humanoid para que las animaciones de la Universal Animation Library se reapliquen a cualquier proporción
    /// (el papá y el Lurker deformado), clips en bucle donde corresponde y sin root motion (el movimiento lo hace el
    /// CharacterController / NavMeshAgent).
    /// </summary>
    public static class QuaterniusImport
    {
        public const string Root = "Assets/ThirdParty/Quaternius";
        public const string Body = Root + "/BaseCharacters/Superhero_Male_FullBody.fbx";
        public const string UAL1 = Root + "/UniversalAnimationLibrary/UAL1_Standard.fbx";
        public const string UAL2 = Root + "/UniversalAnimationLibrary2/UAL2_Standard.fbx";

        [MenuItem("Project Ridge/Characters/Configure Quaternius Imports")]
        public static string Configure()
        {
            var log = new System.Text.StringBuilder();
            foreach (var path in new[] { Body, UAL1, UAL2 })
            {
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.materialImportMode = path == Body ? ModelImporterMaterialImportMode.ImportStandard : ModelImporterMaterialImportMode.None;
                imp.importAnimation = path != Body;
                imp.isReadable = path == Body; // la ropa y el Lurker se generan desde esta malla
                imp.SaveAndReimport();

                if (path != Body)
                {
                    // Bucles y sin movimiento de raíz.
                    var clips = imp.defaultClipAnimations;
                    foreach (var c in clips)
                    {
                        bool loop = c.name.Contains("_Loop") || c.name.EndsWith("Idle");
                        c.loopTime = loop;
                        c.loopPose = loop;
                        c.lockRootRotation = true;
                        c.lockRootHeightY = true;
                        c.lockRootPositionXZ = true;
                        // Orientación según el cuerpo (el FBX mira hacia -Z) y altura desde los pies.
                        c.keepOriginalOrientation = false;
                        c.keepOriginalPositionY = false;
                        c.heightFromFeet = true;
                        c.keepOriginalPositionXZ = false;
                        c.rotationOffset = 0f;
                        c.name = c.name.Replace("Armature|", "");
                    }
                    imp.clipAnimations = clips;
                    imp.SaveAndReimport();
                }
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                log.AppendLine(System.IO.Path.GetFileName(path) + ": avatar valid=" + (avatar != null && avatar.isValid) + " human=" + (avatar != null && avatar.isHuman));
            }

            // Pelo y cejas: modelos sueltos (se cuelgan del hueso de la cabeza).
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/BaseCharacters/Hair" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (ModelImporter)AssetImporter.GetAtPath(p);
                imp.animationType = ModelImporterAnimationType.None;
                imp.importAnimation = false;
                imp.SaveAndReimport();
            }

            // Texturas: normales como normal map.
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/BaseCharacters/Textures" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(p);
                bool normal = p.Contains("_Normal");
                bool rough = p.Contains("_Roughness");
                imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                imp.sRGBTexture = !normal && !rough;
                imp.isReadable = rough;
                imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
            }
            return log.ToString();
        }

        /// <summary>Clips por nombre (sin el prefijo "Armature|") de las dos librerías.</summary>
        public static Dictionary<string, AnimationClip> Clips()
        {
            var d = new Dictionary<string, AnimationClip>();
            foreach (var path in new[] { UAL1, UAL2 })
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                    if (!c.name.StartsWith("__preview")) d[c.name] = c;
            return d;
        }
    }
}
