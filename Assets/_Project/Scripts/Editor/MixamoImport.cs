using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Clips de Mixamo (Adobe; uso libre en proyectos propios, ver ASSETS.md) descargados como "FBX for Unity",
    /// sin piel y con "In Place" en la locomoción. Import Humanoid (el rig de Mixamo se automapea bien), un clip por
    /// archivo con el nombre del archivo, loop en idles y locomoción. Las claves de abajo reemplazan a los clips de
    /// Quaternius en los Animator Controllers (CharacterBuilder) cuando el archivo existe.
    /// </summary>
    public static class MixamoImport
    {
        public const string Root = "Assets/ThirdParty/Mixamo";

        /// <summary>clave usada por CharacterBuilder → fragmento del nombre de archivo de Mixamo (búsqueda sin mayúsculas).</summary>
        public static readonly (string key, string file, bool loop)[] Father =
        {
            ("Idle_Loop", "breathing idle", true),
            // Walk_Loop y Crouch_*: siguen con Quaternius. "walking" del pack trae root motion (no es In Place: en el
            // blend tree daría tirones) y "Crouched Walking" va a ~1.9 m/s, demasiado rápido para sigilo (0.72 m/s).
            ("Jog_Fwd_Loop", "jogging", true),
            ("Sprint_Loop", "unarmed run forward", true),
            ("Sword_Regular_A", "standing melee attack horizontal", false),
            ("Hit_Chest", "hit reaction", false),
            ("Farm_Harvest", "picking up", false),
            ("Chest_Open", "picking up", false),
            ("Death01", "two handed sword death", false),
            ("LayToIdle", "getting up", false),
            ("Mixamo_LookAround", "look around", true),
            ("Mixamo_Electrocuted", "electrocuted", false),
        };

        /// <summary>Claves que usa CharacterBuilder.LurkerController (pendiente de aplicar: primero el papá).</summary>
        public static readonly (string key, string file, bool loop)[] Lurker =
        {
            ("Zombie_Walk_Fwd_Loop", "mutant walking", true),
            ("Jog_Fwd_Loop", "mutant run", true),
            ("Sprint_Loop", "mutant run", true),
            ("Zombie_Idle_Loop", "mutant breathing idle", true),
            ("Zombie_Scratch", "mutant swiping", false),
            ("Death01", "mutant dying", false),
        };

        public static bool NeedsConfigure() =>
            Files().Any(p => (AssetImporter.GetAtPath(p) as ModelImporter)?.animationType != ModelImporterAnimationType.Human);

        static IEnumerable<string> Files() =>
            Directory.Exists(Root) ? Directory.GetFiles(Root, "*.fbx", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')) : Enumerable.Empty<string>();

        public static string FindFile(string fragment)
        {
            var f = fragment.ToLowerInvariant();
            // preferencia: nombre exacto sin extensión; si no, el más corto que contenga el fragmento
            var all = Files().ToList();
            var exact = all.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).ToLowerInvariant() == f);
            if (exact != null) return exact;
            return all.Where(p => Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains(f)).OrderBy(p => p.Length).FirstOrDefault();
        }

        public static AnimationClip Clip(string fragment)
        {
            var path = FindFile(fragment);
            if (path == null) return null;
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
        }

        static bool IsLoop(string file)
        {
            var n = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            return Father.Concat(Lurker).Any(t => t.loop && n.Contains(t.file));
        }

        [MenuItem("Project Ridge/Characters/Configure Mixamo Imports")]
        public static string Configure()
        {
            var log = new System.Text.StringBuilder();
            foreach (var path in Files())
            {
                string clipName = Path.GetFileNameWithoutExtension(path);
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) { AssetDatabase.ImportAsset(path); imp = AssetImporter.GetAtPath(path) as ModelImporter; }
                if (imp == null) { log.AppendLine(clipName + ": sin importer"); continue; }
                bool loop = IsLoop(path);
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.importAnimation = true;
                imp.animationCompression = ModelImporterAnimationCompression.Optimal;
                imp.SaveAndReimport();
                var clips = imp.defaultClipAnimations;
                foreach (var c in clips)
                {
                    c.name = clipName;
                    c.loopTime = loop;
                    c.loopPose = loop;
                    c.lockRootRotation = true;
                    c.lockRootHeightY = true;
                    c.lockRootPositionXZ = true;   // In Place en Mixamo; por si acaso, horneado
                    c.keepOriginalOrientation = true;
                    c.keepOriginalPositionY = false;
                    c.heightFromFeet = true;
                    c.keepOriginalPositionXZ = true;
                }
                imp.clipAnimations = clips;
                imp.SaveAndReimport();
                var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                var clip = Clip(clipName);
                log.AppendLine(clipName + ": human=" + (avatar != null && avatar.isHuman) + " loop=" + loop + " length=" + (clip != null ? clip.length.ToString("0.00") : "-") + " s");
            }
            Debug.Log("MIXAMO IMPORT\n" + log);
            return log.ToString();
        }

        /// <summary>Sustituye en el diccionario de clips (Quaternius) las claves que tienen clip de Mixamo.</summary>
        public static List<string> Override(Dictionary<string, AnimationClip> clips, (string key, string file, bool loop)[] table)
        {
            var done = new List<string>();
            foreach (var (key, file, _) in table)
            {
                var c = Clip(file);
                if (c == null) continue;
                clips[key] = c; done.Add(key);
            }
            return done;
        }
    }
}
