using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Import settings de los clips de 100STYLE (CC BY 4.0, ver ASSETS.md) convertidos con
    /// Tools/Blender/convert_100style.py: Humanoid con mapa de huesos explícito (el esqueleto de 100STYLE llama
    /// "Shoulder" al brazo y "Collar" a la clavícula, el automapeo los confunde), sin dedos.
    /// El giro de la raíz no se hornea en la pose de los clips de transición (arranque, frenada, giro, pivote): queda
    /// como root motion, que el papá descarta, y el giro real lo hace el PlayerController. Así los pies dan los pasos
    /// del giro sin que el cuerpo gire dos veces.
    /// </summary>
    public static class Style100Import
    {
        public const string Root = "Assets/ThirdParty/100STYLE";

        static readonly Dictionary<string, string> Map = new Dictionary<string, string>
        {
            { "Hips", "Hips" }, { "Chest", "Spine" }, { "Chest2", "Chest" }, { "Chest4", "UpperChest" },
            { "Neck", "Neck" }, { "Head", "Head" },
            { "LeftCollar", "LeftShoulder" }, { "LeftShoulder", "LeftUpperArm" }, { "LeftElbow", "LeftLowerArm" }, { "LeftWrist", "LeftHand" },
            { "RightCollar", "RightShoulder" }, { "RightShoulder", "RightUpperArm" }, { "RightElbow", "RightLowerArm" }, { "RightWrist", "RightHand" },
            { "LeftHip", "LeftUpperLeg" }, { "LeftKnee", "LeftLowerLeg" }, { "LeftAnkle", "LeftFoot" }, { "LeftToe", "LeftToes" },
            { "RightHip", "RightUpperLeg" }, { "RightKnee", "RightLowerLeg" }, { "RightAnkle", "RightFoot" }, { "RightToe", "RightToes" },
        };

        static bool Loops(string clip) => clip.EndsWith("Idle");
        // Mirar atrás: el cuerpo gira de verdad y vuelve (giro horneado en la pose, neto ~0°).
        static bool BakeTurn(string clip) => Loops(clip) || clip.EndsWith("LookAround");

        [MenuItem("Project Ridge/Characters/Configure 100STYLE Imports")]
        public static string Configure()
        {
            var log = new System.Text.StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string clipName = System.IO.Path.GetFileNameWithoutExtension(path);
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.importAnimation = true;
                imp.SaveAndReimport();

                // Mapa explícito sobre el esqueleto que Unity ya leyó.
                var hd = imp.humanDescription;
                var names = new HashSet<string>(hd.skeleton.Select(s => s.name));
                hd.human = Map.Where(kv => names.Contains(kv.Key)).Select(kv => new HumanBone
                {
                    boneName = kv.Key, humanName = kv.Value, limit = new HumanLimit { useDefaultValues = true },
                }).ToArray();
                imp.humanDescription = hd;
                imp.SaveAndReimport();

                var clips = imp.defaultClipAnimations;
                foreach (var c in clips)
                {
                    bool loop = Loops(clipName);
                    c.name = clipName;
                    c.loopTime = loop;
                    c.loopPose = loop;
                    c.lockRootRotation = BakeTurn(clipName); // ver el resumen: transiciones sin giro horneado
                    c.lockRootHeightY = true;
                    c.lockRootPositionXZ = false;    // desplazamiento como root motion (se descarta)
                    c.keepOriginalOrientation = false;
                    c.keepOriginalPositionY = false;
                    c.heightFromFeet = true;
                    c.keepOriginalPositionXZ = false;
                }
                imp.clipAnimations = clips;
                imp.SaveAndReimport();
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                var clip = Clip(clipName);
                log.AppendLine(clipName + ": human=" + (avatar != null && avatar.isHuman) + " valid=" + (avatar != null && avatar.isValid)
                               + " length=" + (clip != null ? clip.length.ToString("0.00") : "-") + " s");
            }
            return log.ToString();
        }

        public static AnimationClip Clip(string name) =>
            AssetDatabase.LoadAllAssetsAtPath(Root + "/" + name + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
    }
}
