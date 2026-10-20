using ProjectRidge.Items;
using ProjectRidge.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// POLISH_PASS P1: controles nuevos en el papá de la escena activa. Conecta las acciones de entrada por nombre,
    /// agrega el agachado automático (AutoStance), la ACCIÓN contextual (PlayerActions), el botón OBJETO
    /// (PlayerItems, con su arco de puntería), la sensación de "oculto" (HiddenFeedback) y los topes de inventario.
    /// </summary>
    public static class ControlsBuilder
    {
        const string DataDir = "Assets/_Project/Data";

        [MenuItem("Project Ridge/Player/Setup Controls (POLISH P1)")]
        public static string Setup()
        {
            var pc = Object.FindFirstObjectByType<PlayerController>();
            var player = pc.gameObject;
            var input = player.GetComponent<PlayerInputHandler>();
            var stealth = AssetDatabase.LoadAssetAtPath<StealthConfig>(DataDir + "/StealthConfig.asset");
            var combatConfig = AssetDatabase.LoadAssetAtPath<CombatConfig>(DataDir + "/CombatConfig.asset");

            // Entrada: cada campo con su acción por nombre.
            var iso = new SerializedObject(input);
            foreach (var (field, action) in new[] { ("move", "Move"), ("sprint", "Sprint"), ("crouch", "Crouch"), ("action", "Action"), ("useObject", "Object"),
                                                    ("cycleObject", "CycleObject"), ("flashlight", "Flashlight"), ("pause", "Pause"), ("heal", "Heal") })
                iso.FindProperty(field).objectReferenceValue = ItemsSetupBuilder.ActionRef(action);
            iso.ApplyModifiedPropertiesWithoutUndo();

            // Topes de inventario (sección 3.3).
            foreach (var (item, max) in new[] { ("Stone", 3), ("Flare", 2), ("Medkit", 2) })
            {
                var def = ItemsSetupBuilder.LoadItem(item);
                def.maxCarry = max;
                EditorUtility.SetDirty(def);
            }

            var stance = Ensure<AutoStance>(player);
            Set(stance, ("config", stealth), ("player", pc), ("input", input), ("visibility", player.GetComponent<PlayerVisibility>()));
            Set(pc, ("stance", stance));

            var actions = Ensure<PlayerActions>(player);
            Set(actions, ("config", combatConfig), ("input", input), ("controller", pc), ("interactor", player.GetComponent<PlayerInteractor>()),
                ("combat", player.GetComponent<PlayerCombat>()), ("health", player.GetComponent<PlayerHealth>()));

            var items = Ensure<PlayerItems>(player);
            var arcTr = player.transform.Find("AimArc");
            if (arcTr == null) { arcTr = new GameObject("AimArc").transform; arcTr.SetParent(player.transform, false); }
            if (!arcTr.TryGetComponent(out LineRenderer arc)) arc = arcTr.gameObject.AddComponent<LineRenderer>();
            arc.useWorldSpace = true;
            arc.widthMultiplier = 0.05f;
            arc.numCapVertices = 2;
            arc.shadowCastingMode = ShadowCastingMode.Off;
            arc.receiveShadows = false;
            arc.sharedMaterial = ArcMaterial();
            arc.enabled = false;
            var so = new SerializedObject(items);
            so.FindProperty("config").objectReferenceValue = combatConfig;
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("controller").objectReferenceValue = pc;
            so.FindProperty("inventory").objectReferenceValue = player.GetComponent<PlayerInventory>();
            so.FindProperty("thrower").objectReferenceValue = player.GetComponent<PlayerThrow>();
            so.FindProperty("combat").objectReferenceValue = player.GetComponent<PlayerCombat>();
            so.FindProperty("aimArc").objectReferenceValue = arc;
            so.FindProperty("flareItem").objectReferenceValue = ItemsSetupBuilder.LoadItem("Flare");
            var list = so.FindProperty("items");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = ItemsSetupBuilder.LoadItem("Stone");
            list.GetArrayElementAtIndex(1).objectReferenceValue = ItemsSetupBuilder.LoadItem("Flare");
            so.ApplyModifiedPropertiesWithoutUndo();

            // Oculto: viñeta, paso bajo y silueta.
            var feedback = Ensure<HiddenFeedback>(player);
            var listener = Object.FindFirstObjectByType<AudioListener>();
            if (!listener.TryGetComponent(out AudioLowPassFilter lowPass)) lowPass = listener.gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;
            lowPass.enabled = false;
            var fso = new SerializedObject(feedback);
            fso.FindProperty("visibility").objectReferenceValue = player.GetComponent<PlayerVisibility>();
            fso.FindProperty("vignette").objectReferenceValue = HiddenVolume();
            fso.FindProperty("lowPass").objectReferenceValue = lowPass;
            var visual = player.transform.Find("Visual");
            var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            var body = fso.FindProperty("body");
            body.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) body.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            fso.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            return "controls: stance, actions, items, hidden feedback on " + player.name;
        }

        static Volume HiddenVolume()
        {
            var go = GameObject.Find("HiddenVolume") ?? new GameObject("HiddenVolume");
            if (!go.TryGetComponent(out Volume v)) v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 50f;
            v.weight = 0f;
            string path = "Assets/_Project/Art/Hollow/HiddenVignette.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            if (!profile.TryGet(out Vignette vig)) vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.48f);
            vig.smoothness.Override(0.65f);
            vig.color.Override(Color.black);
            if (!profile.TryGet(out ColorAdjustments ca)) ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(-0.25f);
            EditorUtility.SetDirty(profile);
            v.sharedProfile = profile;
            return v;
        }

        static Material ArcMaterial()
        {
            string path = "Assets/_Project/Art/Hollow/AimArc.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", new Color(1f, 0.85f, 0.55f, 1f));
            EditorUtility.SetDirty(m);
            return m;
        }

        static T Ensure<T>(GameObject go) where T : Component => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

        static void Set(Object target, params (string field, Object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in values) so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
