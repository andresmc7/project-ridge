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
            foreach (var n in new[] { "StickBase", "Attack", "Interact", "Crouch", "Sprint", "Pause", "TouchControls", "StealthHud", "SurvivalHud", "DamageFlash", "Fire", "Throw" })
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
            Button("Fire", touch, br, new Vector2(-150f, 460f), 86f, "FLARE", "<Gamepad>/rightShoulder", disc, ring);
            Button("Throw", touch, br, new Vector2(-300f, 420f), 86f, "THROW", "<Gamepad>/leftShoulder", disc, ring);
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
            var weapon = Label("Weapon", survival, Vector2.zero, new Vector2(420f, 26f), "", 16, FontStyle.Bold);
            Anchor(weapon.rectTransform, new Vector2(0f, 1f), new Vector2(260f, -100f));
            weapon.alignment = TextAnchor.MiddleLeft;
            weapon.color = new Color(1f, 1f, 1f, 0.7f);
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
            sso.FindProperty("weaponLabel").objectReferenceValue = weapon;
            sso.FindProperty("combat").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerCombat>();
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

        // ------------------------------------------------------------------ Flujo de juego (título, pausa, pista, final)

        [MenuItem("Project Ridge/HUD/Rebuild Game Flow UI in Active Scene")]
        public static string BuildFlow()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var white = SpriteAsset("UI_White", (u, v) => 1f);
            var disc = SpriteAsset("UI_Disc", (u, v) => Disc(u, v, 1f));
            var canvas = GameObject.Find("HUD").transform;
            var safe = canvas.Find("SafeArea");
            foreach (var n in new[] { "Flow", "Objective" })
            {
                var old = canvas.Find(n) ?? safe.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var oldFlow = GameObject.Find("GameFlow");
            if (oldFlow != null) Object.DestroyImmediate(oldFlow);

            if (!safe.TryGetComponent(out CanvasGroup hudGroup)) hudGroup = safe.gameObject.AddComponent<CanvasGroup>();

            var objective = Label("Objective", (RectTransform)safe, Vector2.zero, new Vector2(520f, 26f), "", 16, FontStyle.Italic);
            Anchor(objective.rectTransform, new Vector2(0f, 1f), new Vector2(310f, -126f));
            objective.alignment = TextAnchor.MiddleLeft;

            var flow = Stretch("Flow", canvas);

            // Mensaje grande (CHECKPOINT, YOU DIED, TAKEN).
            var big = Label("BigMessage", flow, new Vector2(0f, 60f), new Vector2(900f, 90f), "", 54, FontStyle.Bold);

            // Pista
            var clue = Stretch("CluePanel", flow);
            var clueBg = Img(clue, white, new Color(0.02f, 0.025f, 0.03f, 0.86f), true);
            var clueBtn = clue.gameObject.AddComponent<Button>();
            clueBtn.transition = Selectable.Transition.None;
            var clueGroup = clue.gameObject.AddComponent<CanvasGroup>();
            var clueTitle = Label("Title", clue, new Vector2(0f, 150f), new Vector2(900f, 50f), "", 30, FontStyle.Bold);
            clueTitle.color = new Color(0.95f, 0.85f, 0.6f, 1f);
            var clueBody = Label("Body", clue, new Vector2(0f, -10f), new Vector2(860f, 260f), "", 22, FontStyle.Normal);
            clueBody.color = new Color(0.9f, 0.9f, 0.86f, 0.95f);
            clueBody.lineSpacing = 1.15f;
            var clueHint = Label("Hint", clue, new Vector2(0f, -190f), new Vector2(600f, 30f), "[E]  /  tap to continue", 16, FontStyle.Normal);
            clueHint.color = new Color(1f, 1f, 1f, 0.5f);

            // Pausa
            var pause = Stretch("PausePanel", flow);
            Img(pause, white, new Color(0f, 0f, 0f, 0.72f), true);
            var pauseGroup = pause.gameObject.AddComponent<CanvasGroup>();
            Label("Title", pause, new Vector2(0f, 190f), new Vector2(600f, 60f), "PAUSED", 40, FontStyle.Bold);
            var resume = MenuButton(pause, "Resume", new Vector2(0f, 90f), white);
            var restart = MenuButton(pause, "Restart", new Vector2(0f, 20f), white);
            var quit = MenuButton(pause, "Quit", new Vector2(0f, -50f), white);
            Label("VolumeLabel", pause, new Vector2(0f, -125f), new Vector2(300f, 26f), "VOLUME", 16, FontStyle.Bold).color = new Color(1f, 1f, 1f, 0.6f);
            var slider = VolumeSlider(pause, new Vector2(0f, -160f), white, disc);
            Label("Controls", pause, new Vector2(0f, -245f), new Vector2(1100f, 60f),
                "WASD move  ·  Shift sprint  ·  C crouch  ·  E use  ·  LMB club  ·  G throw stone  ·  Q flare  ·  H medkit  ·  F flashlight", 15, FontStyle.Normal).color = new Color(1f, 1f, 1f, 0.45f);

            // Final
            var end = Stretch("EndPanel", flow);
            var endGroup = end.gameObject.AddComponent<CanvasGroup>();
            var endBody = Label("Body", end, new Vector2(0f, 60f), new Vector2(980f, 520f), "", 24, FontStyle.Normal);
            endBody.supportRichText = true;
            endBody.color = new Color(0.9f, 0.88f, 0.82f, 1f);
            var endButtonsRt = Rect("Buttons", end, new Vector2(0.5f, 0f), new Vector2(0f, 120f), 0f);
            endButtonsRt.sizeDelta = new Vector2(600f, 80f);
            var endButtons = endButtonsRt.gameObject.AddComponent<CanvasGroup>();
            endGroup.alpha = 0f;
            endButtons.alpha = 0f;
            endButtons.interactable = false;
            endButtons.blocksRaycasts = false;
            var playAgain = MenuButton(endButtonsRt, "Play again", new Vector2(-150f, 0f), white);
            var endQuit = MenuButton(endButtonsRt, "Quit", new Vector2(150f, 0f), white);

            // Fundido (encima de todo menos el título)
            var fadeRt = Stretch("Fade", flow);
            var fadeImg = Img(fadeRt, white, Color.black, false);
            var fade = fadeRt.gameObject.AddComponent<CanvasGroup>();
            fade.blocksRaycasts = false;
            fade.interactable = false;
            endGroupToFront(end);
            big.transform.SetAsLastSibling(); // "YOU DIED" se lee sobre el negro

            // Título
            var title = Stretch("TitlePanel", flow);
            var titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            Label("Studio", title, new Vector2(0f, 120f), new Vector2(600f, 30f), "P R O J E C T   R I D G E", 16, FontStyle.Bold).color = new Color(1f, 1f, 1f, 0.45f);
            var titleText = Label("Title", title, new Vector2(0f, 50f), new Vector2(1000f, 120f), "THE HOLLOW", 88, FontStyle.Bold);
            titleText.color = new Color(0.92f, 0.9f, 0.84f, 1f);
            Label("Tagline", title, new Vector2(0f, -25f), new Vector2(900f, 34f), "They were here. Find them.", 22, FontStyle.Italic).color = new Color(0.85f, 0.8f, 0.7f, 0.8f);
            Label("Prompt", title, new Vector2(0f, -170f), new Vector2(700f, 30f), "press any button  ·  tap to begin", 18, FontStyle.Normal).color = new Color(1f, 1f, 1f, 0.55f);
            Label("Hint", title, new Vector2(0f, -205f), new Vector2(900f, 26f), "headphones recommended", 14, FontStyle.Italic).color = new Color(1f, 1f, 1f, 0.35f);

            // GameFlow
            var player = Object.FindFirstObjectByType<ProjectRidge.Player.PlayerHealth>();
            var go = new GameObject("GameFlow");
            var gf = go.AddComponent<ProjectRidge.Core.GameFlow>();
            var so = new SerializedObject(gf);
            so.FindProperty("player").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerController>();
            so.FindProperty("health").objectReferenceValue = player;
            so.FindProperty("input").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerInputHandler>();
            so.FindProperty("body").objectReferenceValue = player.GetComponent<CharacterController>();
            so.FindProperty("healthConfig").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ProjectRidge.Player.PlayerHealthConfig>("Assets/_Project/Data/PlayerHealthConfig.asset");
            so.FindProperty("ufoExposure").objectReferenceValue = Object.FindFirstObjectByType<ProjectRidge.AI.UFOExposure>();
            var spawn = GameObject.Find("PlayerSpawn");
            so.FindProperty("firstCheckpoint").objectReferenceValue = spawn != null ? spawn.transform : null;
            so.FindProperty("gameplayHud").objectReferenceValue = hudGroup;
            so.FindProperty("fade").objectReferenceValue = fade;
            so.FindProperty("fadeImage").objectReferenceValue = fadeImg;
            so.FindProperty("titlePanel").objectReferenceValue = titleGroup;
            so.FindProperty("pausePanel").objectReferenceValue = pauseGroup;
            so.FindProperty("volumeSlider").objectReferenceValue = slider;
            so.FindProperty("cluePanel").objectReferenceValue = clueGroup;
            so.FindProperty("clueTitle").objectReferenceValue = clueTitle;
            so.FindProperty("clueBody").objectReferenceValue = clueBody;
            so.FindProperty("bigMessage").objectReferenceValue = big;
            so.FindProperty("objectiveLabel").objectReferenceValue = objective;
            so.FindProperty("endPanel").objectReferenceValue = endGroup;
            so.FindProperty("endBody").objectReferenceValue = endBody;
            so.FindProperty("endButtons").objectReferenceValue = endButtons;
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.Events.UnityEventTools.AddPersistentListener(resume.onClick, gf.Resume);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(restart.onClick, gf.Restart);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(quit.onClick, gf.Quit);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(playAgain.onClick, gf.Restart);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(endQuit.onClick, gf.Quit);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(clueBtn.onClick, gf.DismissClue);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "Game flow UI built in " + scene.path;
        }

        // El panel final se dibuja sobre el fundido (el texto queda legible con la pantalla en negro).
        static void endGroupToFront(RectTransform end) => end.SetAsLastSibling();

        static Button MenuButton(RectTransform parent, string label, Vector2 pos, Sprite white)
        {
            var rt = Rect(label.Replace(" ", ""), parent, new Vector2(0.5f, 0.5f), pos, 0f);
            rt.sizeDelta = new Vector2(260f, 52f);
            var img = Img(rt, white, new Color(1f, 1f, 1f, 0.08f), true);
            var b = rt.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 2.2f);
            colors.pressedColor = new Color(1f, 1f, 1f, 3f);
            colors.selectedColor = new Color(1f, 1f, 1f, 2.2f);
            b.colors = colors;
            b.targetGraphic = img;
            var t = Label("Label", rt, Vector2.zero, rt.sizeDelta, label.ToUpperInvariant(), 20, FontStyle.Bold);
            t.color = new Color(1f, 1f, 1f, 0.9f);
            return b;
        }

        static Slider VolumeSlider(RectTransform parent, Vector2 pos, Sprite white, Sprite disc)
        {
            var rt = Rect("Volume", parent, new Vector2(0.5f, 0.5f), pos, 0f);
            rt.sizeDelta = new Vector2(300f, 30f);
            var bg = Rect("Background", rt, new Vector2(0.5f, 0.5f), Vector2.zero, 0f);
            bg.anchorMin = new Vector2(0f, 0.4f); bg.anchorMax = new Vector2(1f, 0.6f); bg.offsetMin = bg.offsetMax = Vector2.zero;
            Img(bg, white, new Color(1f, 1f, 1f, 0.15f), false);
            var fillArea = Rect("FillArea", rt, new Vector2(0.5f, 0.5f), Vector2.zero, 0f);
            fillArea.anchorMin = new Vector2(0f, 0.4f); fillArea.anchorMax = new Vector2(1f, 0.6f); fillArea.offsetMin = fillArea.offsetMax = Vector2.zero;
            var fill = Rect("Fill", fillArea, new Vector2(0.5f, 0.5f), Vector2.zero, 0f);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(1f, 1f); fill.offsetMin = fill.offsetMax = Vector2.zero;
            Img(fill, white, new Color(0.95f, 0.85f, 0.6f, 0.8f), false);
            var handleArea = Rect("HandleArea", rt, new Vector2(0.5f, 0.5f), Vector2.zero, 0f);
            handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one; handleArea.offsetMin = new Vector2(12f, 0f); handleArea.offsetMax = new Vector2(-12f, 0f);
            var handle = Rect("Handle", handleArea, new Vector2(0.5f, 0.5f), Vector2.zero, 28f);
            var handleImg = Img(handle, disc, Color.white, true);
            var s = rt.gameObject.AddComponent<Slider>();
            s.fillRect = fill;
            s.handleRect = handle;
            s.targetGraphic = handleImg;
            s.minValue = 0f;
            s.maxValue = 1f;
            s.value = 1f;
            return s;
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

        static readonly string[] ShownItems = { "Medkit", "Flare", "Stone" };

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
