using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Reconstruye el HUD de la escena activa: controles táctiles circulares y discretos (solo en móvil o editor)
    /// y el indicador de sigilo SAFE / ALERT / HUNTED. Conserva el DebugOverlay.
    /// </summary>
    public static class HudBuilder
    {
        const string UiDir = "Assets/_Project/Art/UI";
        static Font font;

        [MenuItem("Project Ridge/HUD/Rebuild HUD in Active Scene")]
        public static string Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var disc = SpriteAsset("UI_Disc", (u, v) => Disc(u, v, 1f));
            var ring = SpriteAsset("UI_Ring", (u, v) => Disc(u, v, 1f) - Disc(u, v, 0.86f));
            var white = SpriteAsset("UI_White", (u, v) => 1f);

            var canvas = GameObject.Find("HUD");
            var safe = canvas.transform.Find("SafeArea");
            foreach (var n in new[] { "StickBase", "Attack", "Interact", "Crouch", "Sprint", "Pause", "TouchControls", "StealthHud", "SurvivalHud", "DamageFlash" })
            {
                var old = safe.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            // --- Destello de daño (detrás de todo lo demás)
            var flashRt = Stretch("DamageFlash", safe);
            flashRt.SetAsFirstSibling();
            Img(flashRt, white, new Color(0.45f, 0f, 0f, 0.35f), false);
            var flash = flashRt.gameObject.AddComponent<CanvasGroup>();
            flash.alpha = 0f;
            flash.interactable = false;
            flash.blocksRaycasts = false;

            // --- Controles táctiles
            var touch = Stretch("TouchControls", safe);
            touch.gameObject.AddComponent<ProjectRidge.UI.TouchControlsVisibility>();

            var stickBase = Rect("StickBase", touch, new Vector2(0f, 0f), new Vector2(200f, 200f), 230f);
            Img(stickBase, disc, new Color(0f, 0f, 0f, 0.18f), false);
            var stickRing = Rect("Ring", stickBase, new Vector2(0.5f, 0.5f), Vector2.zero, 230f);
            Img(stickRing, ring, new Color(1f, 1f, 1f, 0.35f), false);
            var knob = Rect("Stick", stickBase, new Vector2(0.5f, 0.5f), Vector2.zero, 92f);
            Img(knob, disc, new Color(1f, 1f, 1f, 0.28f), true);
            var knobRing = Rect("Ring", knob, new Vector2(0.5f, 0.5f), Vector2.zero, 92f);
            Img(knobRing, ring, new Color(1f, 1f, 1f, 0.6f), false);
            var stick = knob.gameObject.AddComponent<OnScreenStick>();
            stick.controlPath = "<Gamepad>/leftStick";
            stick.movementRange = 70f;

            Vector2 br = new Vector2(1f, 0f);
            Button("Attack", touch, br, new Vector2(-150f, 165f), 124f, "ATTACK", "<Gamepad>/rightTrigger", disc, ring);
            Button("Interact", touch, br, new Vector2(-300f, 120f), 98f, "USE", "<Gamepad>/buttonSouth", disc, ring);
            Button("Crouch", touch, br, new Vector2(-150f, 320f), 98f, "CROUCH", "<Gamepad>/buttonEast", disc, ring);
            Button("Sprint", touch, br, new Vector2(-300f, 272f), 98f, "SPRINT", "<Gamepad>/leftStickPress", disc, ring);
            Button("Heal", touch, br, new Vector2(-430f, 110f), 80f, "HEAL", "<Gamepad>/dpad/up", disc, ring);
            Button("Pause", touch, new Vector2(1f, 1f), new Vector2(-64f, -60f), 64f, "II", "<Gamepad>/start", disc, ring);

            // --- Vida y recursos (arriba a la izquierda), aviso de interacción y mensajes (abajo al centro)
            var survival = Stretch("SurvivalHud", safe);
            var healthBg = Rect("HealthBg", survival, new Vector2(0f, 1f), new Vector2(170f, -48f), 0f);
            healthBg.sizeDelta = new Vector2(240f, 6f);
            Img(healthBg, white, new Color(1f, 1f, 1f, 0.14f), false);
            var healthRt = Rect("HealthFill", healthBg, new Vector2(0.5f, 0.5f), Vector2.zero, 0f);
            healthRt.anchorMin = Vector2.zero; healthRt.anchorMax = Vector2.one; healthRt.offsetMin = healthRt.offsetMax = Vector2.zero;
            var healthFill = Img(healthRt, white, new Color(0.86f, 0.82f, 0.74f, 0.9f), false);
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            var resources = Label("Resources", survival, Vector2.zero, new Vector2(420f, 28f), "", 17, FontStyle.Bold);
            Anchor(resources.rectTransform, new Vector2(0f, 1f), new Vector2(260f, -76f));
            resources.alignment = TextAnchor.MiddleLeft;
            var prompt = Label("Prompt", survival, Vector2.zero, new Vector2(600f, 34f), "", 22, FontStyle.Bold);
            Anchor(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 150f));
            var toast = Label("Toast", survival, Vector2.zero, new Vector2(600f, 30f), "", 19, FontStyle.Normal);
            Anchor(toast.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 196f));

            var survivalHud = survival.gameObject.AddComponent<ProjectRidge.UI.SurvivalHud>();
            var sso = new SerializedObject(survivalHud);
            var player = Object.FindFirstObjectByType<ProjectRidge.Player.PlayerHealth>();
            sso.FindProperty("health").objectReferenceValue = player;
            sso.FindProperty("inventory").objectReferenceValue = player.GetComponent<ProjectRidge.Items.PlayerInventory>();
            sso.FindProperty("interactor").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerInteractor>();
            var items = sso.FindProperty("shownItems");
            items.arraySize = ShownItems.Length;
            for (int i = 0; i < ShownItems.Length; i++)
                items.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<ProjectRidge.Items.ItemDefinition>("Assets/_Project/Data/Items/" + ShownItems[i] + ".asset");
            sso.FindProperty("healthFill").objectReferenceValue = healthFill;
            sso.FindProperty("resourcesLabel").objectReferenceValue = resources;
            sso.FindProperty("promptLabel").objectReferenceValue = prompt;
            sso.FindProperty("toastLabel").objectReferenceValue = toast;
            sso.FindProperty("damageFlash").objectReferenceValue = flash;
            sso.ApplyModifiedPropertiesWithoutUndo();

            // --- Indicador de sigilo (arriba al centro)
            var hud = Rect("StealthHud", safe, new Vector2(0.5f, 1f), new Vector2(0f, -62f), 0f);
            hud.sizeDelta = new Vector2(420f, 90f);
            var group = hud.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            var state = Label("State", hud, new Vector2(0f, 18f), new Vector2(420f, 36f), "SAFE", 28, FontStyle.Bold);
            var barBg = Rect("MeterBg", hud, new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), 0f);
            barBg.sizeDelta = new Vector2(240f, 5f);
            Img(barBg, white, new Color(1f, 1f, 1f, 0.14f), false);
            var fillRt = Rect("MeterFill", barBg, new Vector2(0.5f, 0.5f), Vector2.zero, 0f);
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one; fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
            var fill = Img(fillRt, white, Color.white, false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            var detail = Label("Detail", hud, new Vector2(0f, -30f), new Vector2(420f, 24f), "", 16, FontStyle.Normal);

            var stealth = hud.gameObject.AddComponent<ProjectRidge.UI.StealthHud>();
            var so = new SerializedObject(stealth);
            so.FindProperty("stateLabel").objectReferenceValue = state;
            so.FindProperty("detailLabel").objectReferenceValue = detail;
            so.FindProperty("meterFill").objectReferenceValue = fill;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("ufoExposure").objectReferenceValue = Object.FindFirstObjectByType<ProjectRidge.AI.UFOExposure>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "HUD rebuilt in " + scene.path;
        }

        static float Disc(float u, float v, float radius)
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f; // 0 centro, 1 borde
            return Mathf.Clamp01((radius - d) * 128f); // borde suavizado (~1 px a 256)
        }

        static Sprite SpriteAsset(string name, System.Func<float, float, float> alpha)
        {
            PolyHavenMaterialBuilder.EnsureFolder(UiDir);
            string path = UiDir + "/" + name + ".png";
            if (!File.Exists(path))
            {
                const int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha((x + 0.5f) / size, (y + 0.5f) / size)));
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = TextureImporterType.Sprite;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.zero, 0f);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            return rt;
        }

        static readonly string[] ShownItems = { "Medkit", "Flare", "Scrap" };

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
        }

        static Image Img(RectTransform rt, Sprite sprite, Color color, bool raycast)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        static Text Label(string name, RectTransform parent, Vector2 pos, Vector2 size, string text, int fontSize, FontStyle style)
        {
            var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), pos, 0f);
            rt.sizeDelta = size;
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(1f, 1f, 1f, 0.85f);
            t.raycastTarget = false;
            return t;
        }

        static void Button(string name, RectTransform parent, Vector2 anchor, Vector2 pos, float size, string label, string controlPath, Sprite disc, Sprite ring)
        {
            var rt = Rect(name, parent, anchor, pos, size);
            Img(rt, disc, new Color(0f, 0f, 0f, 0.32f), true);
            var r = Rect("Ring", rt, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Img(r, ring, new Color(1f, 1f, 1f, 0.5f), false);
            var b = rt.gameObject.AddComponent<OnScreenButton>();
            b.controlPath = controlPath;
            var t = Label("Label", rt, Vector2.zero, new Vector2(size, size), label, label.Length <= 2 ? 22 : 17, FontStyle.Bold);
            t.color = new Color(1f, 1f, 1f, 0.8f);
        }
    }
}
