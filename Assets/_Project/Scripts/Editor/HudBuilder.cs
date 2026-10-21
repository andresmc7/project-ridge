using System.Collections.Generic;
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
    /// y el HUD mínimo de 3.12 (corazón, viñeta, aviso de recogida, indicadores de los Lurker). Conserva el DebugOverlay.
    /// </summary>
    public static class HudBuilder
    {
        const string UiDir = "Assets/_Project/Art/UI";
        static Font font;

        [MenuItem("Project Ridge/HUD/Rebuild HUD in Active Scene")]
        public static string Build()
        {
            font = HudFont();
            var disc = SpriteAsset("UI_Disc", (u, v) => Disc(u, v, 1f));
            var ring = SpriteAsset("UI_Ring", (u, v) => Disc(u, v, 1f) - Disc(u, v, 0.86f));
            var white = SpriteAsset("UI_White", (u, v) => 1f);

            var canvas = GameObject.Find("HUD");
            var safe = canvas.transform.Find("SafeArea");
            foreach (var n in new[] { "StickBase", "Attack", "Interact", "Crouch", "Sprint", "Pause", "TouchControls", "StickArea", "Action", "Object", "Flashlight", "HealHold", "StealthHud", "SurvivalHud", "DamageFlash", "Fire", "Throw", "DetectionIndicators" })
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

            // POLISH_PASS 3.1: un pulgar mueve (joystick flotante en el 45% izquierdo), el otro decide (ACCIÓN + OBJETO).
            // Sin botones de agacharse ni de sprint. Tamaños en pt de iPhone convertidos a unidades del canvas.
            var playerGo = Object.FindFirstObjectByType<ProjectRidge.Player.PlayerController>().gameObject;
            var stickArea = Rect("StickArea", touch, Vector2.zero, Vector2.zero, 0f);
            stickArea.anchorMin = Vector2.zero; stickArea.anchorMax = new Vector2(StickAreaWidth, 1f);
            stickArea.offsetMin = stickArea.offsetMax = Vector2.zero;
            Img(stickArea, white, new Color(0f, 0f, 0f, 0f), true); // invisible, recibe el toque
            var stickRing = Rect("Ring", stickArea, new Vector2(0f, 0f), Vector2.zero, Pt(70f) * 2f);
            stickRing.anchorMin = stickRing.anchorMax = new Vector2(0f, 0f);
            var ringImg = Img(stickRing, ring, new Color(1f, 1f, 1f, 0.22f), false);
            var knob = Rect("Knob", stickRing, new Vector2(0.5f, 0.5f), Vector2.zero, Pt(26f));
            Img(knob, disc, new Color(1f, 1f, 1f, 0.25f), false);
            // El anillo se posiciona en coordenadas locales del área: pivote en su esquina inferior izquierda.
            stickArea.pivot = Vector2.zero;
            var floating = stickArea.gameObject.AddComponent<ProjectRidge.UI.FloatingStick>();
            var fso = new SerializedObject(floating);
            fso.FindProperty("input").objectReferenceValue = playerGo.GetComponent<ProjectRidge.Player.PlayerInputHandler>();
            fso.FindProperty("ring").objectReferenceValue = stickRing;
            fso.FindProperty("knob").objectReferenceValue = knob;
            fso.FindProperty("ringImage").objectReferenceValue = ringImg;
            fso.FindProperty("range").floatValue = Pt(70f);
            fso.ApplyModifiedPropertiesWithoutUndo();

            Vector2 br = new Vector2(1f, 0f);
            // ACCIÓN: grande, abajo a la derecha. El toque va al botón A del gamepad virtual.
            float actionSize = Pt(110f);
            var actionRt = Rect("Action", touch, br, new Vector2(-Pt(79f), Pt(79f)), actionSize);
            Img(actionRt, disc, new Color(0f, 0f, 0f, 0.35f), true);
            Img(Rect("Ring", actionRt, new Vector2(0.5f, 0.5f), Vector2.zero, actionSize), ring, new Color(1f, 1f, 1f, 0.55f), false);
            actionRt.gameObject.AddComponent<OnScreenButton>().controlPath = "<Gamepad>/buttonSouth";
            var actionGroup = actionRt.gameObject.AddComponent<CanvasGroup>();
            var actionLabel = Label("Label", actionRt, Vector2.zero, new Vector2(actionSize, actionSize), "", 34, FontStyle.Bold);
            var ab = actionRt.gameObject.AddComponent<ProjectRidge.UI.ActionButton>();
            var abso = new SerializedObject(ab);
            abso.FindProperty("actions").objectReferenceValue = playerGo.GetComponent<ProjectRidge.Player.PlayerActions>();
            abso.FindProperty("group").objectReferenceValue = actionGroup;
            abso.FindProperty("label").objectReferenceValue = actionLabel;
            abso.ApplyModifiedPropertiesWithoutUndo();

            // OBJETO: mediano, encima y a la izquierda de ACCIÓN.
            float objectSize = Pt(84f);
            var objectRt = Rect("Object", touch, br, new Vector2(-Pt(178f), Pt(160f)), objectSize);
            Img(objectRt, disc, new Color(0f, 0f, 0f, 0.35f), true);
            Img(Rect("Ring", objectRt, new Vector2(0.5f, 0.5f), Vector2.zero, objectSize), ring, new Color(1f, 1f, 1f, 0.5f), false);
            var objectGroup = objectRt.gameObject.AddComponent<CanvasGroup>();
            var objectLabel = Label("Label", objectRt, new Vector2(0f, 14f), new Vector2(objectSize, 50f), "", 28, FontStyle.Bold);
            var objectCount = Label("Count", objectRt, new Vector2(0f, -40f), new Vector2(objectSize, 50f), "", 36, FontStyle.Bold);
            objectCount.color = new Color(1f, 0.86f, 0.6f, 0.95f);
            var objectIcon = Img(Rect("Icon", objectRt, new Vector2(0.5f, 0.5f), new Vector2(0f, Pt(8f)), objectSize * 0.5f), null, new Color(0.95f, 0.92f, 0.86f, 0.95f), false);
            objectIcon.preserveAspect = true;
            var icons = ItemIcons();
            var ob = objectRt.gameObject.AddComponent<ProjectRidge.UI.ObjectButton>();
            var obso = new SerializedObject(ob);
            obso.FindProperty("items").objectReferenceValue = playerGo.GetComponent<ProjectRidge.Player.PlayerItems>();
            obso.FindProperty("label").objectReferenceValue = objectLabel;
            obso.FindProperty("count").objectReferenceValue = objectCount;
            obso.FindProperty("icon").objectReferenceValue = objectIcon;
            obso.FindProperty("group").objectReferenceValue = objectGroup;
            obso.FindProperty("cancelRadius").floatValue = objectSize * 0.5f;
            obso.FindProperty("aimDeadzone").floatValue = Pt(10f);
            obso.FindProperty("swipeDistance").floatValue = Pt(24f);
            obso.ApplyModifiedPropertiesWithoutUndo();

            // Íconos pequeños arriba a la derecha: linterna y pausa.
            Button("Flashlight", touch, new Vector2(1f, 1f), new Vector2(-Pt(84f), -Pt(30f)), Pt(40f), "LIGHT", "<Gamepad>/buttonNorth", disc, ring);
            Button("Pause", touch, new Vector2(1f, 1f), new Vector2(-Pt(34f), -Pt(30f)), Pt(40f), "II", "<Gamepad>/start", disc, ring);

            // Curarse: mantener el indicador de vida (arriba a la izquierda).
            var healRt = Rect("HealHold", touch, new Vector2(0f, 1f), new Vector2(Pt(34f), -Pt(30f)), Pt(44f));
            Img(healRt, disc, new Color(0f, 0f, 0f, 0.001f), true);
            var healFill = Img(Rect("Progress", healRt, new Vector2(0.5f, 0.5f), Vector2.zero, Pt(44f)), ring, new Color(0.9f, 0.3f, 0.25f, 0.9f), false);
            healFill.type = Image.Type.Filled;
            healFill.fillMethod = Image.FillMethod.Radial360;
            healFill.fillAmount = 0f;
            // Vida (3.12): un corazón pequeño, solo con la vida < 100% o al tocarlo (lo maneja SurvivalHud).
            var heartRt = Rect("Heart", healRt, new Vector2(0.5f, 0.5f), Vector2.zero, Pt(26f));
            var heartImg = Img(heartRt, SpriteAsset("UI_Heart", Heart), Color.white, false);
            var heartGroup = heartRt.gameObject.AddComponent<CanvasGroup>();
            heartGroup.alpha = 0f;
            var hh = healRt.gameObject.AddComponent<ProjectRidge.UI.HoldToHeal>();
            var hso = new SerializedObject(hh);
            hso.FindProperty("healing").objectReferenceValue = playerGo.GetComponent<ProjectRidge.Player.PlayerHealing>();
            hso.FindProperty("progress").objectReferenceValue = healFill;
            hso.ApplyModifiedPropertiesWithoutUndo();

            // --- Vida y recursos (arriba a la izquierda), aviso de interacción y mensajes (abajo al centro)
            // 3.12: sin contadores permanentes ni textos de estado. Viñeta roja con poca vida (pantalla completa).
            var vignetteOld = canvas.transform.Find("LowHealthVignette");
            if (vignetteOld != null) Object.DestroyImmediate(vignetteOld.gameObject);
            var vignetteRt = Stretch("LowHealthVignette", canvas.transform);
            vignetteRt.SetAsFirstSibling();
            Img(vignetteRt, SpriteAsset("UI_Vignette", (u, v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.98f, Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 1.41f))),
                new Color(0.55f, 0f, 0f, 1f), false);
            var vignette = vignetteRt.gameObject.AddComponent<CanvasGroup>();
            vignette.alpha = 0f;
            vignette.blocksRaycasts = false;

            var survival = Stretch("SurvivalHud", safe);
            var prompt = Label("Prompt", survival, Vector2.zero, new Vector2(900f, 50f), "", 22, FontStyle.Bold);
            Anchor(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, Pt(64f)));
            var toast = Label("Toast", survival, Vector2.zero, new Vector2(900f, 50f), "", 19, FontStyle.Normal);
            Anchor(toast.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, Pt(84f)));
            // Aviso de recogida: ícono + "+1" a la izquierda del botón OBJETO, 1.2 s.
            var pickupRt = Rect("Pickup", survival, br, new Vector2(-Pt(248f), Pt(160f)), 0f);
            pickupRt.sizeDelta = new Vector2(Pt(70f), Pt(30f));
            var pickupGroup = pickupRt.gameObject.AddComponent<CanvasGroup>();
            pickupGroup.alpha = 0f;
            var pickupIcon = Img(Rect("Icon", pickupRt, new Vector2(0f, 0.5f), new Vector2(Pt(14f), 0f), Pt(26f)), null, new Color(1f, 0.92f, 0.78f, 1f), false);
            pickupIcon.preserveAspect = true;
            var pickupLabel = Label("Count", pickupRt, Vector2.zero, new Vector2(Pt(70f), Pt(30f)), "+1", 24, FontStyle.Bold);
            Anchor(pickupLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-Pt(18f), 0f));
            pickupLabel.color = new Color(1f, 0.92f, 0.78f, 1f);

            var survivalHud = survival.gameObject.AddComponent<ProjectRidge.UI.SurvivalHud>();
            var sso = new SerializedObject(survivalHud);
            var player = Object.FindFirstObjectByType<ProjectRidge.Player.PlayerHealth>();
            sso.FindProperty("health").objectReferenceValue = player;
            sso.FindProperty("inventory").objectReferenceValue = player.GetComponent<ProjectRidge.Items.PlayerInventory>();
            sso.FindProperty("interactor").objectReferenceValue = player.GetComponent<ProjectRidge.Player.PlayerInteractor>();
            sso.FindProperty("healHold").objectReferenceValue = hh;
            sso.FindProperty("heart").objectReferenceValue = heartGroup;
            sso.FindProperty("heartImage").objectReferenceValue = heartImg;
            sso.FindProperty("lowHealthVignette").objectReferenceValue = vignette;
            sso.FindProperty("pickup").objectReferenceValue = pickupGroup;
            sso.FindProperty("pickupIcon").objectReferenceValue = pickupIcon;
            sso.FindProperty("pickupLabel").objectReferenceValue = pickupLabel;
            sso.FindProperty("promptLabel").objectReferenceValue = prompt;
            sso.FindProperty("toastLabel").objectReferenceValue = toast;
            sso.FindProperty("damageFlash").objectReferenceValue = flash;
            sso.ApplyModifiedPropertiesWithoutUndo();
            foreach (var kv in icons)
            {
                var item = AssetDatabase.LoadAssetAtPath<ProjectRidge.Items.ItemDefinition>("Assets/_Project/Data/Items/" + kv.Key + ".asset");
                if (item == null) continue;
                item.icon = kv.Value;
                EditorUtility.SetDirty(item);
            }

            // --- Indicadores de detección sobre los Lurker (y flecha en el borde si están fuera de cámara).
            var arrowSprite = SpriteAsset("UI_Arrow", (u, v) => Mathf.Clamp01((v * 0.9f - Mathf.Abs(u - 0.5f) * 1.8f + 0.05f) * 40f) * (v < 0.92f ? 1f : 0f));
            var detect = Stretch("DetectionIndicators", safe);
            var arcRt = Rect("Arc", detect, new Vector2(0.5f, 0.5f), Vector2.zero, Pt(18f));
            var arc = Img(arcRt, ring, Color.white, false);
            arc.type = Image.Type.Filled;
            arc.fillMethod = Image.FillMethod.Radial360;
            arc.fillOrigin = (int)Image.Origin360.Bottom;
            arc.fillAmount = 0f;
            var arrowRt = Rect("EdgeArrow", detect, new Vector2(0.5f, 0.5f), Vector2.zero, Pt(14f));
            var arrow = Img(arrowRt, arrowSprite, Color.white, false);
            var di = detect.gameObject.AddComponent<ProjectRidge.UI.DetectionIndicators>();
            var dso = new SerializedObject(di);
            dso.FindProperty("arcTemplate").objectReferenceValue = arc;
            dso.FindProperty("arrowTemplate").objectReferenceValue = arrow;
            dso.FindProperty("edgeInset").floatValue = Pt(26f);
            dso.ApplyModifiedPropertiesWithoutUndo();

            // Sin indicador de sigilo arriba (SAFE/HIDDEN/ALERT/HUNTED, 3.12): la lectura la dan los ojos e indicadores
            // de los Lurker (P5) y el agacharse del papá en la vegetación.

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "HUD rebuilt in " + scene.path;
        }

        // ------------------------------------------------------------------ Flujo de juego (título, pausa, pista, final)

        [MenuItem("Project Ridge/HUD/Rebuild Game Flow UI in Active Scene")]
        public static string BuildFlow()
        {
            font = HudFont();
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

            // Objetivo (3.12): una frase corta arriba al centro; GameFlow la muestra 4 s y en pausa.
            var objective = Label("Objective", (RectTransform)safe, Vector2.zero, new Vector2(1100f, 60f), "", 40, FontStyle.Normal);
            Anchor(objective.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -Pt(30f)));
            objective.alignment = TextAnchor.MiddleCenter;

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
            var resume = MenuButton(pause, "Resume", new Vector2(0f, 120f), white);
            var restart = MenuButton(pause, "Restart", new Vector2(0f, 55f), white);
            // Accesibilidad (POLISH_PASS 3.5): cono de visión de los Lurker en el suelo, apagado por defecto.
            var aids = MenuButton(pause, "Stealth aids: off", new Vector2(0f, -10f), white);
            var quit = MenuButton(pause, "Quit", new Vector2(0f, -75f), white);
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
            so.FindProperty("stealthAidsLabel").objectReferenceValue = aids.GetComponentInChildren<Text>();
            so.ApplyModifiedPropertiesWithoutUndo();

            UnityEditor.Events.UnityEventTools.AddPersistentListener(resume.onClick, gf.Resume);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(restart.onClick, gf.Restart);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(quit.onClick, gf.Quit);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(aids.onClick, gf.ToggleStealthAids);
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

        /// <summary>Unidades del canvas (referencia 1920×1080, mezcla 0.5) por punto de iPhone en horizontal (852×393 pt a 3x).</summary>
        const float CanvasUnitsPerPt = 2.49f;
        static float Pt(float points) => points * CanvasUnitsPerPt;
        /// <summary>Ancho de la zona del joystick flotante (fracción de la pantalla, desde la izquierda).</summary>
        const float StickAreaWidth = 0.45f;

        /// <summary>Corazón (curva implícita clásica), con borde suave.</summary>
        static float Heart(float u, float v)
        {
            float x = (u - 0.5f) * 2.6f, y = (v - 0.45f) * 2.6f;
            float a = x * x + y * y - 1f;
            float f = a * a * a - x * x * y * y * y;
            return Mathf.Clamp01(-f * 6f);
        }

        /// <summary>Íconos de los objetos (siluetas simples, blancas; el color lo da la Image).</summary>
        static Dictionary<string, Sprite> ItemIcons()
        {
            float Box(float u, float v, float cx, float cy, float hw, float hh) =>
                Mathf.Clamp01((Mathf.Min(hw - Mathf.Abs(u - cx), hh - Mathf.Abs(v - cy))) * 60f);
            return new Dictionary<string, Sprite>
            {
                // Piedra: canto rodado irregular.
                { "Stone", SpriteAsset("UI_Icon_Stone", (u, v) =>
                {
                    float x = (u - 0.5f) / 0.42f, y = (v - 0.42f) / 0.3f;
                    float r = Mathf.Sqrt(x * x + y * y) - 0.08f * Mathf.Sin(Mathf.Atan2(y, x) * 3f + 0.7f);
                    return Mathf.Clamp01((1f - r) * 20f);
                }) },
                // Bengala: cartucho y llama.
                { "Flare", SpriteAsset("UI_Icon_Flare", (u, v) =>
                {
                    float body = Box(u, v, 0.5f, 0.32f, 0.11f, 0.24f);
                    float fx = (u - 0.5f) / 0.13f, fy = (v - 0.7f) / 0.2f;
                    float flame = Mathf.Clamp01((1f - Mathf.Sqrt(fx * fx * (1f + fy) + fy * fy)) * 12f) * (v > 0.55f ? 1f : 0f);
                    return Mathf.Max(body, flame);
                }) },
                // Botiquín: cruz.
                { "Medkit", SpriteAsset("UI_Icon_Medkit", (u, v) => Mathf.Max(Box(u, v, 0.5f, 0.5f, 0.12f, 0.36f), Box(u, v, 0.5f, 0.5f, 0.36f, 0.12f))) },
            };
        }

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

        /// <summary>Única fuente del HUD (3.12): Barlow Condensed SemiBold, OFL (ver ASSETS.md).</summary>
        static Font HudFont() => AssetDatabase.LoadAssetAtPath<Font>("Assets/ThirdParty/Fonts/BarlowCondensed/BarlowCondensed-SemiBold.ttf");

        /// <summary>Tamaño mínimo de texto: 14 pt en un iPhone de 6.1" (3.12).</summary>
        static int MinFont => Mathf.CeilToInt(Pt(14f));

        static Text Label(string name, RectTransform parent, Vector2 pos, Vector2 size, string text, int fontSize, FontStyle style)
        {
            var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), pos, 0f);
            rt.sizeDelta = size;
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = Mathf.Max(fontSize, MinFont);
            // La fuente ya es semi negrita: sin negrita sintética encima.
            t.fontStyle = style == FontStyle.Italic ? FontStyle.Italic : FontStyle.Normal;
            t.verticalOverflow = VerticalWrapMode.Overflow;
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
