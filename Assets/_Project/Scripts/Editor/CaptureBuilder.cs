using ProjectRidge.Core;
using ProjectRidge.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// POLISH_PASS P2: monta la secuencia de captura del OVNI en la escena activa: CaptureConfig, pose de shock en el
    /// papá, Volume de la captura (aberración, grano, desaturación, viñeta fría), anillo del forcejeo y destello blanco
    /// en el HUD, crepitar eléctrico y la conexión con GameFlow. Va después de HudBuilder.BuildFlow (que recrea GameFlow).
    /// </summary>
    public static class CaptureBuilder
    {
        const string ConfigPath = "Assets/_Project/Data/CaptureConfig.asset";
        const string ProfilePath = "Assets/_Project/Art/Hollow/CaptureVolume.asset";

        [MenuItem("Project Ridge/The Hollow/Setup UFO Capture (POLISH P2)")]
        public static string Setup()
        {
            var config = AssetDatabase.LoadAssetAtPath<CaptureConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CaptureConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            var pc = Object.FindFirstObjectByType<PlayerController>();
            var player = pc.gameObject;
            var father = player.GetComponentInChildren<FatherAnimator>();
            if (!father.TryGetComponent(out ShockPose shock)) shock = father.gameObject.AddComponent<ShockPose>();

            var flowGo = Object.FindFirstObjectByType<GameFlow>().gameObject;
            if (!flowGo.TryGetComponent(out CaptureSequence seq)) seq = flowGo.AddComponent<CaptureSequence>();
            if (!flowGo.TryGetComponent(out AudioSource crackle)) crackle = flowGo.AddComponent<AudioSource>();
            crackle.spatialBlend = 0f;
            crackle.volume = 0.55f;
            crackle.playOnAwake = false;

            // HUD: anillo del forcejeo y destello blanco, sobre todo lo demás.
            var canvas = GameObject.Find("HUD").transform;
            var old = canvas.Find("CaptureUI");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = NewRect("CaptureUI", canvas);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            root.SetAsLastSibling();
            var ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/UI_Ring.png");
            var whiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/UI_White.png");
            var ring = NewRect("StruggleRing", root);
            ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f); // la posición en pantalla se convierte relativa al centro
            ring.sizeDelta = Vector2.one * 220f;
            var ringGroup = ring.gameObject.AddComponent<CanvasGroup>();
            ringGroup.blocksRaycasts = false;
            var back = ring.gameObject.AddComponent<Image>();
            back.sprite = ringSprite;
            back.color = new Color(1f, 1f, 1f, 0.18f);
            back.raycastTarget = false;
            var fillRt = NewRect("Fill", ring);
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one; fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
            var fill = fillRt.gameObject.AddComponent<Image>();
            fill.sprite = ringSprite;
            fill.color = new Color(0.85f, 0.93f, 1f, 0.95f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;
            var flash = NewRect("WhiteFlash", root);
            flash.anchorMin = Vector2.zero; flash.anchorMax = Vector2.one; flash.offsetMin = flash.offsetMax = Vector2.zero;
            var flashImg = flash.gameObject.AddComponent<Image>();
            flashImg.sprite = whiteSprite;
            flashImg.color = Color.white;
            flashImg.raycastTarget = false;
            var flashGroup = flash.gameObject.AddComponent<CanvasGroup>();
            flashGroup.alpha = 0f;
            flashGroup.blocksRaycasts = false;

            var so = new SerializedObject(seq);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("player").objectReferenceValue = pc;
            so.FindProperty("body").objectReferenceValue = player.GetComponent<CharacterController>();
            so.FindProperty("input").objectReferenceValue = player.GetComponent<PlayerInputHandler>();
            so.FindProperty("father").objectReferenceValue = father;
            so.FindProperty("shock").objectReferenceValue = shock;
            so.FindProperty("flashlight").objectReferenceValue = player.GetComponent<FlashlightController>();
            so.FindProperty("ufoExposure").objectReferenceValue = Object.FindFirstObjectByType<ProjectRidge.AI.UFOExposure>();
            so.FindProperty("volume").objectReferenceValue = CaptureVolume();
            so.FindProperty("struggleFill").objectReferenceValue = fill;
            so.FindProperty("struggleGroup").objectReferenceValue = ringGroup;
            so.FindProperty("whiteFlash").objectReferenceValue = flashGroup;
            so.FindProperty("crackle").objectReferenceValue = crackle;
            so.ApplyModifiedPropertiesWithoutUndo();

            var gso = new SerializedObject(flowGo.GetComponent<GameFlow>());
            gso.FindProperty("capture").objectReferenceValue = seq;
            gso.ApplyModifiedPropertiesWithoutUndo();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "ufo capture sequence ready";
        }

        static Volume CaptureVolume()
        {
            var go = GameObject.Find("CaptureVolume") ?? new GameObject("CaptureVolume");
            if (!go.TryGetComponent(out Volume v)) v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 60f;
            v.weight = 0f;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            if (!profile.TryGet(out ChromaticAberration ca)) ca = profile.Add<ChromaticAberration>(true);
            ca.intensity.Override(1f);
            if (!profile.TryGet(out FilmGrain grain)) grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium3);
            grain.intensity.Override(0.85f);
            if (!profile.TryGet(out ColorAdjustments adj)) adj = profile.Add<ColorAdjustments>(true);
            adj.saturation.Override(-70f); // saturación al 0.3
            if (!profile.TryGet(out Vignette vig)) vig = profile.Add<Vignette>(true);
            vig.color.Override(new Color(0.55f, 0.7f, 1f));
            vig.intensity.Override(0.45f);
            vig.smoothness.Override(0.55f);
            EditorUtility.SetDirty(profile);
            v.sharedProfile = profile;
            return v;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }
    }
}
