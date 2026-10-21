using System.Collections;
using System.Collections.Generic;
using ProjectRidge.AI;
using ProjectRidge.Core;
using ProjectRidge.Items;
using ProjectRidge.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Pruebas de aceptación del POLISH PASS (docs/POLISH_PASS.md, sección 5), una rutina por fase. Corren en play
    /// mode con dispositivos virtuales, escriben "POLISH: PASS/FAIL ..." en la consola y guardan capturas en
    /// docs/progress/. Uso: PolishTests.Run("P0").
    /// </summary>
    public static class PolishTests
    {
        public const string ProgressDir = "C:/Dev/ProjectRidge/docs/progress/";

        static readonly List<string> results = new List<string>();
        static IEnumerator routine;
        static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        static float waitUntil;
        static Gamepad pad;
        static Keyboard keyboard;
        static PlayerController player;
        static string phase;

        public static bool Running { get; private set; }

        public static void Run(string which)
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("POLISH: entra en play mode primero"); return; }
            pad = InputSystem.AddDevice<Gamepad>("PolishPad");
            keyboard = InputSystem.AddDevice<Keyboard>("PolishKeyboard");
            player = Object.FindFirstObjectByType<PlayerController>();
            results.Clear();
            phase = which;
            stack.Clear();
            routine = which switch
            {
                "P0" => P0(),
                "P1" => P1(),
                "P2" => P2(),
                "P3" => P3(),
                "P4" => P4(),
                "P5" => P5(),
                "P6" => P6(),
                _ => Unknown(),
            };
            stack.Push(routine);
            waitUntil = 0f;
            Running = true;
            EditorApplication.update += Tick;
            Debug.Log("POLISH: start " + which);
        }

        static IEnumerator Unknown()
        {
            Check("phase", false, "no hay pruebas para " + phase);
            yield break;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { Stop(); return; }
            if (Time.time < waitUntil) return;
            try
            {
                // Pila de rutinas: "yield return OtraRutina()" se ejecuta anidada.
                while (true)
                {
                    var top = stack.Peek();
                    if (!top.MoveNext())
                    {
                        stack.Pop();
                        if (stack.Count == 0) { Finish(); return; }
                        continue;
                    }
                    if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                    if (top.Current is float s) waitUntil = Time.time + s;
                    return;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("POLISH: exception " + e);
                Finish();
            }
        }

        static void Stop()
        {
            EditorApplication.update -= Tick;
            Running = false;
            if (pad != null) { InputSystem.RemoveDevice(pad); pad = null; }
            if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
        }

        static void Finish()
        {
            Stop();
            int fails = 0;
            foreach (var r in results) if (r.StartsWith("FAIL")) fails++;
            Debug.Log("POLISH: DONE " + phase + " " + (results.Count - fails) + "/" + results.Count + " passed\n" + string.Join("\n", results));
        }

        static void Check(string name, bool ok, string detail)
        {
            string line = (ok ? "PASS " : "FAIL ") + name + " — " + detail;
            results.Add(line);
            Debug.Log("POLISH: " + line);
        }

        // ------------------------------------------------------------------ Utilidades

        static void Tap(Key k)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(k));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }

        static void Stick(Vector2 v)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = v });
        }

        static void Teleport(Vector3 p)
        {
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = p;
            cc.enabled = true;
            var rescue = player.GetComponent<PlayerFallRecovery>();
            if (rescue != null) rescue.Forget();
        }

        static Vector3 Ground(float x, float z)
        {
            if (NavMesh.SamplePosition(new Vector3(x, 1f, z), out NavMeshHit hit, 3f, NavMesh.AllAreas)) return hit.position + Vector3.up * 0.05f;
            var t = Terrain.activeTerrain;
            var p = new Vector3(x, 0f, z);
            p.y = t.SampleHeight(p) + t.transform.position.y + 0.05f;
            return p;
        }

        static void Shot(string name) => Shots.Capture(ProgressDir + name + ".png", player.transform.position);

        static IEnumerator PassTitle()
        {
            Tap(Key.Space);
            yield return 1.5f;
            if (GameFlow.Instance != null && GameFlow.Instance.Current != GameFlow.State.Playing) { Tap(Key.Space); yield return 1f; }
            float limit = Time.time + 10f;
            while (player.InputLocked && Time.time < limit) yield return 0.1f; // el fundido del título bloquea la entrada
        }

        static void Calm()
        {
            Searchlight.SuppressAll(9999f);
            foreach (var e in EnemyController.Active) e.Perception.Suppress(9999f);
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // ------------------------------------------------------------------ P0

        static IEnumerator P0()
        {
            // D14: en el título no hay botones de fin.
            var end = GameObject.Find("EndPanel");
            float endAlpha = 0f;
            if (end != null) foreach (var g in end.GetComponentsInChildren<CanvasGroup>()) endAlpha = Mathf.Max(endAlpha, g.alpha * end.GetComponent<CanvasGroup>().alpha);
            Check("D14 no end buttons on title", end != null && endAlpha < 0.01f, "state " + GameFlow.Instance.Current + ", end panel alpha " + endAlpha);
            yield return PassTitle();
            Calm();

            // D1: ningún clip de locomoción del papá fuera de 0.85–1.1x.
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Project/Art/Characters/Father.controller");
            var lctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Project/Art/Characters/Lurker.controller");
            var rates = new List<string>();
            bool ratesOk = true;
            foreach (var c in new[] { ctrl, lctrl })
                foreach (var st in c.layers[0].stateMachine.states)
                    if (st.state.motion is BlendTree bt)
                        foreach (var ch in bt.children)
                        {
                            if (ch.motion.name.Contains("Idle")) continue;
                            rates.Add(c.name + "/" + ch.motion.name + "@" + ch.threshold.ToString("0.00") + "=" + ch.timeScale.ToString("0.00") + "x");
                            ratesOk &= ch.timeScale >= CharacterBuilder.MinClipRate - 0.001f && ch.timeScale <= CharacterBuilder.MaxClipRate + 0.001f && st.state.speed == 1f;
                        }
            Check("D1 clip rates within 0.85–1.1x", ratesOk, string.Join(", ", rates));

            // D4: caminar y trotar sin garrote en la mano.
            var combat = player.GetComponent<PlayerCombat>();
            combat.RestoreClub();
            var anim = player.GetComponentInChildren<FatherAnimator>();
            var animator = anim.GetComponent<Animator>();
            var handR = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var club = Find(anim.transform, "Club");
            Teleport(Ground(-54f, 0f));
            yield return 0.5f;
            Stick(new Vector2(1f, 0f));
            yield return 2f;
            float runSpeed = player.CurrentSpeed;
            bool clubOnBack = club != null && club.gameObject.activeInHierarchy && !club.IsChildOf(handR);
            Shot("P0_after_run");
            Stick(Vector2.zero);
            Check("D4 club sheathed while moving", clubOnBack && !combat.ClubInHand, "club parent " + (club != null ? club.parent.name : "none") + ", speed " + runSpeed.ToString("0.00"));
            yield return 0.5f;

            // D2: giro de 180° en ~0.33 s (540°/s), no instantáneo.
            Stick(new Vector2(-1f, 0f));
            float t0 = Time.time, turned = 0f;
            Vector3 f0 = player.transform.forward;
            while (Time.time - t0 < 1.5f)
            {
                if (Vector3.Angle(player.transform.forward, f0) > 170f) { turned = Time.time - t0; break; }
                yield return null;
            }
            Stick(Vector2.zero);
            Check("D2 turn speed 540°/s", turned > 0.25f && turned < 0.6f, "180° in " + turned.ToString("0.00") + " s (config " + player.Config.turnSpeed + "°/s)");
            yield return 0.6f;

            // D3: recoger del suelo (agacharse) con su clip, y captura en el momento más bajo.
            var pickups = Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None);
            Pickup nearest = null;
            float best = 999f;
            foreach (var p in pickups)
            {
                float d = Vector3.Distance(p.transform.position, player.transform.position);
                if (d < best) { best = d; nearest = p; }
            }
            bool pickState = false;
            float hipDrop = 0f;
            if (nearest != null)
            {
                Vector3 at = nearest.transform.position;
                Teleport(Ground(at.x - 0.7f, at.z));
                player.FaceTowards(at);
                yield return 0.6f;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                float standHip = hips.position.y - player.transform.position.y;
                Tap(Key.E);
                float tEnd = Time.time + 1.6f;
                bool shot = false;
                while (Time.time < tEnd)
                {
                    pickState |= animator.GetCurrentAnimatorStateInfo(0).IsName("PickUp");
                    float drop = standHip - (hips.position.y - player.transform.position.y);
                    if (drop > hipDrop) hipDrop = drop;
                    if (!shot && drop > 0.2f) { Shot("P0_after_pickup"); shot = true; }
                    yield return null;
                }
            }
            Check("D3 ground pick-up", pickState && hipDrop > 0.2f, "state PickUp " + pickState + ", hips dropped " + hipDrop.ToString("0.00") + " m");

            // D4: ante un Lurker cazando cerca, desenfunda solo; sin amenaza, enfunda.
            EnemyController lurker = null;
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (e.name == "Lurker") { lurker = e; e.gameObject.SetActive(true); }
                else e.gameObject.SetActive(false);
            }
            Teleport(Ground(-8f, 0f));
            yield return 0.3f;
            lurker.Perception.Suppress(0f);
            var lp = lurker.transform.position;
            Teleport(Ground(lp.x + 4f, lp.z));
            lurker.AlertTo(player.transform.position);
            float drawnAt = -1f;
            float tLimit = Time.time + 10f;
            while (Time.time < tLimit)
            {
                if (lurker.IsHunting && combat.ClubInHand && club.IsChildOf(handR)) { drawnAt = Time.time; break; }
                yield return null;
            }
            Check("D4 draws club on threat", drawnAt > 0f, "lurker " + lurker.State + ", draw " + combat.DrawProgress.ToString("0.00"));
            lurker.ResetAfterPlayerRespawn(player.transform.position, 999f, 0f);
            lurker.Perception.Suppress(9999f);
            Teleport(Ground(-20.6f, -3.3f));
            yield return 4.5f;
            Check("D4 sheathes after threat", !combat.ClubInHand && !club.IsChildOf(handR), "draw " + combat.DrawProgress.ToString("0.00") + ", parent " + club.parent.name);

            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) e.gameObject.SetActive(true);

            // Sin chatarra ni fabricación.
            bool noScrap = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Scrap.asset") == null
                           && GameObject.Find("Pickup_Scrap") == null;
            Check("scrap and crafting removed", noScrap, "scrap asset/pickups gone " + noScrap);
        }

        // ------------------------------------------------------------------ P1

        static IEnumerator Hold(Vector2 stick, float seconds)
        {
            Stick(stick);
            yield return seconds;
        }

        static IEnumerator P1()
        {
            yield return PassTitle();
            var stance = player.GetComponent<AutoStance>();
            var vis = player.GetComponent<PlayerVisibility>();
            var input = player.GetComponent<PlayerInputHandler>();

            // Sin botones de agacharse ni de sprint en táctil; solo joystick + ACCIÓN + OBJETO (+ linterna y pausa).
            var touch = GameObject.Find("TouchControls");
            var paths = new List<string>();
            if (touch != null)
                foreach (var b in touch.GetComponentsInChildren<UnityEngine.InputSystem.OnScreen.OnScreenButton>(true)) paths.Add(b.controlPath);
            bool noCrouchSprint = touch != null && !paths.Contains("<Gamepad>/buttonEast") && !paths.Contains("<Gamepad>/leftStickPress")
                                  && touch.GetComponentInChildren<UI.ActionButton>(true) != null && touch.GetComponentInChildren<UI.ObjectButton>(true) != null
                                  && touch.GetComponentInChildren<UI.FloatingStick>(true) != null;
            Check("touch: no crouch/sprint buttons", noCrouchSprint, "on-screen buttons: " + string.Join(", ", paths));

            // Sin amenazas: sigiloso de pie (lento), caminar y trotar por inclinación.
            Calm();
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) e.gameObject.SetActive(false);
            Teleport(Ground(-54f, 0f));
            yield return 0.5f;
            yield return Hold(new Vector2(0.25f, 0f), 1.2f);
            var sneakMode = player.Mode; float sneakSpeed = player.CurrentSpeed; bool sneakCrouch = player.IsCrouching;
            yield return Hold(new Vector2(0.6f, 0f), 1.2f);
            var walkMode = player.Mode; float walkSpeed = player.CurrentSpeed;
            yield return Hold(new Vector2(0.9f, 0f), 1.2f);
            var jogMode = player.Mode; float jogSpeed = player.CurrentSpeed;
            Check("stick tilt: sneak / walk / jog", sneakMode == MovementMode.Sneak && !sneakCrouch && walkMode == MovementMode.Walk && jogMode == MovementMode.Jog
                  && sneakSpeed < walkSpeed && walkSpeed < jogSpeed,
                  "0.25 → " + sneakMode + " " + sneakSpeed.ToString("0.00") + " m/s (crouch " + sneakCrouch + "), 0.6 → " + walkMode + " " + walkSpeed.ToString("0.00") + ", 0.9 → " + jogMode + " " + jogSpeed.ToString("0.00"));

            // Sprint por empuje al borde (0.25 s); se suelta al frenar.
            yield return Hold(new Vector2(1f, 0f), 0.15f);
            bool earlySprint = player.Mode == MovementMode.Sprint;
            yield return 0.4f;
            bool sprinting = player.Mode == MovementMode.Sprint;
            float sprintSpeed = player.CurrentSpeed;
            yield return Hold(new Vector2(0.6f, 0f), 0.4f);
            bool released = player.Mode != MovementMode.Sprint;
            Stick(Vector2.zero);
            Check("sprint by pushing to the edge", !earlySprint && sprinting && released, "at 0.15 s " + earlySprint + ", at 0.55 s " + sprinting + " (" + sprintSpeed.ToString("0.00") + " m/s), after easing off " + !released);

            // Teclado: W siempre al borde, no fija sprint; Shift sí.
            Teleport(Ground(-54f, 0f));
            yield return 0.3f;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return 1f;
            bool kbJog = player.Mode == MovementMode.Jog;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.LeftShift));
            yield return 0.5f;
            bool kbSprint = player.Mode == MovementMode.Sprint;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Check("keyboard: W jogs, Shift sprints", kbJog && kbSprint, "D → jog " + kbJog + ", D+Shift → sprint " + kbSprint);
            yield return 0.5f;

            // Vegetación densa: se agacha solo y queda oculto; un paso fuera, se levanta (con histéresis).
            Teleport(Ground(-20.6f, -3.3f));
            yield return 0.8f;
            bool inGrassCrouch = player.IsCrouching && stance.Why == AutoStance.Reason.Vegetation && vis.IsHidden;
            Shot("P1_after_hidden");
            Teleport(Ground(-24f, 0f));
            yield return 0.2f;
            bool stillCrouchedBriefly = player.IsCrouching;
            yield return 0.6f;
            bool stoodUp = !player.IsCrouching;
            Check("auto crouch in vegetation", inGrassCrouch && stillCrouchedBriefly && stoodUp,
                  "in grass: crouch " + inGrassCrouch + " (hidden " + vis.IsHidden + "), 0.2 s out still crouched " + stillCrouchedBriefly + ", 0.8 s out standing " + stoodUp);

            // Amenaza cerca: tras cobertura baja se agacha; con el stick apenas inclinado camina agachado.
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) e.gameObject.SetActive(e.name == "Lurker");
            EnemyController lurker = null;
            foreach (var e in EnemyController.Active) lurker = e;
            lurker.Perception.Suppress(9999f);
            Vector3 coverPos = Vector3.zero; string coverName = "none";
            foreach (var go in new[] { "CoverRock", "FallenLog", "CoverLog" })
            {
                var c = GameObject.Find(go);
                if (c != null) { coverPos = c.GetComponentInChildren<Collider>().bounds.center; coverName = go; break; }
            }
            bool coverCrouch = false;
            AutoStance.Reason coverWhy = AutoStance.Reason.None;
            if (coverName != "none")
            {
                // Al lado de la cobertura (a ~0.7 m de su borde, del lado del papá).
                var col = GameObject.Find(coverName).GetComponentInChildren<Collider>();
                Vector3 probe = coverPos + Vector3.right * 3f;
                Vector3 edge = col.ClosestPoint(new Vector3(probe.x, coverPos.y, probe.z));
                Vector3 stand = edge + (probe - edge).normalized * 0.6f;
                Teleport(Ground(stand.x, stand.z));
                lurker.GetComponent<NavMeshAgent>().Warp(Ground(stand.x + 9f, stand.z));
                yield return 1f;
                coverCrouch = player.IsCrouching;
                coverWhy = stance.Why;
            }
            Check("auto crouch behind cover with a threat", coverCrouch && coverWhy == AutoStance.Reason.Cover, coverName + ": crouch " + coverCrouch + " (" + coverWhy + "), threat " + stance.ThreatNear);

            lurker.GetComponent<NavMeshAgent>().Warp(Ground(-5f, 0f));
            Teleport(Ground(-13f, 0f));
            yield return 0.6f;
            yield return Hold(new Vector2(0.25f, 0f), 1f);
            bool sneakCrouched = player.IsCrouching && stance.Why == AutoStance.Reason.Sneak;
            yield return Hold(new Vector2(0.9f, 0f), 0.6f);
            bool upToJog = !player.IsCrouching;
            Stick(Vector2.zero);
            Check("sneak crouched with a threat, stand to jog", sneakCrouched && upToJog, "sneak crouch " + sneakCrouched + " (" + stance.Why + "), jog stands " + upToJog);

            // ACCIÓN contextual: golpe con el Lurker delante; derribo agachado por la espalda (y no muere).
            var actions = player.GetComponent<PlayerActions>();
            var combat = player.GetComponent<PlayerCombat>();
            combat.RestoreClub();
            lurker.ResetAfterPlayerRespawn(player.transform.position, 0f, 9999f);
            yield return 0.5f;
            Vector3 lp = lurker.transform.position;
            Vector3 behind = lp - lurker.transform.forward * 1.2f;
            Teleport(Ground(behind.x, behind.z));
            player.FaceTowards(lp);
            Tap(Key.C); // forzar agachado (solo pruebas) para el derribo
            yield return 0.15f;
            var ctxBehind = actions.Current;
            int durBefore = combat.ClubDurability;
            Tap(Key.E);
            yield return 0.6f;
            bool down = lurker.IsDown;
            Tap(Key.C); // soltar el agachado forzado
            Check("ACTION = takedown from behind", ctxBehind == PlayerActions.Context.Takedown && down && combat.ClubDurability == durBefore - 1,
                  "context " + ctxBehind + ", lurker down " + down + " (" + lurker.State + "), durability " + durBefore + "→" + combat.ClubDurability);
            yield return 2f;
            Check("lurker never dies", !lurker.IsDead && lurker.GetComponent<EnemyHealth>().Current >= 1f, "state " + lurker.State + ", hp " + lurker.GetComponent<EnemyHealth>().Current);

            // OBJETO: piedra al objetivo inteligente (más allá del Lurker), luego la bengala.
            var inv = player.GetComponent<PlayerInventory>();
            var items = player.GetComponent<PlayerItems>();
            var stone = ItemsSetupBuilder.LoadItem("Stone");
            var flare = ItemsSetupBuilder.LoadItem("Flare");
            int added = inv.Add(stone, 5);
            Check("inventory caps (stone 3)", inv.Count(stone) == 3, "added " + added + " of 5, carrying " + inv.Count(stone));
            lurker.ResetAfterPlayerRespawn(player.transform.position, 0f, 9999f);
            Teleport(Ground(-13f, 0f));
            lurker.GetComponent<NavMeshAgent>().Warp(Ground(-6f, 0f));
            yield return 0.6f;
            player.FaceTowards(lurker.transform.position);
            if (items.Active != stone) Tap(Key.Tab);
            Vector3 aim = items.SmartTarget();
            int stonesBefore = inv.Count(stone);
            Tap(Key.Q);
            yield return 1.5f;
            float beyond = Vector3.Distance(Flat(aim), Flat(player.transform.position)) - Vector3.Distance(Flat(lurker.transform.position), Flat(player.transform.position));
            Check("OBJECT: stone to smart target", inv.Count(stone) == stonesBefore - 1 && beyond > 1f, "stones " + stonesBefore + "→" + inv.Count(stone) + ", lands " + beyond.ToString("0.0") + " m beyond the lurker");
            inv.Add(flare, 1);
            Tap(Key.Tab);
            yield return 0.2f;
            bool flareActive = items.Active == flare;
            Tap(Key.Q);
            yield return 1.5f;
            Check("OBJECT: cycle + flare gun", flareActive && inv.Count(flare) == 0 && Object.FindObjectsByType<FlareProjectile>(FindObjectsSortMode.None).Length > 0,
                  "flare active " + flareActive + ", flares left " + inv.Count(flare));
            Shot("P1_after");
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) e.gameObject.SetActive(true);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        // ------------------------------------------------------------------ P3

        /// <summary>Alto del papá en pantalla (fracción del alto de la imagen) con la cámara de juego.</summary>
        static float DadScreenHeight()
        {
            var anim = player.GetComponentInChildren<FatherAnimator>().GetComponent<Animator>();
            var cam = Camera.main;
            Vector3 head = anim.GetBoneTransform(HumanBodyBones.Head).position + Vector3.up * 0.14f;
            Vector3 foot = player.transform.position;
            return Mathf.Abs(cam.WorldToViewportPoint(head).y - cam.WorldToViewportPoint(foot).y);
        }

        /// <summary>Puntos de paso del sendero (fuera de la vegetación: el papá de pie), uno por sala.</summary>
        public static readonly Vector2[] LookSpots = { new Vector2(-55f, 0f), new Vector2(-24f, 0f), new Vector2(-1.5f, 0.2f), new Vector2(24f, 0f), new Vector2(47.6f, -0.5f) };

        static IEnumerator P3()
        {
            yield return PassTitle();
            Calm();
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) e.gameObject.SetActive(false);
            var rig = Object.FindFirstObjectByType<ProjectRidge.CameraRig.IsoCameraRig>();

            // Escala del personaje y luz: en varias salas, quieto.
            float hMin = 1f, hMax = 0f, darkSum = 0f, brightSum = 0f;
            int n = 0;
            foreach (var s in LookSpots)
            {
                Teleport(Ground(s.x, s.y));
                yield return 1.5f;
                float h = DadScreenHeight();
                hMin = Mathf.Min(hMin, h); hMax = Mathf.Max(hMax, h);
                string file = ProgressDir + "P3_after_" + n + ".png";
                Shots.Capture(file, player.transform.position);
                var (dark, bright, mean) = Shots.Histogram(file);
                Debug.Log("POLISH: P3 shot " + n + " at " + s + ": dad " + (h * 100f).ToString("0.0") + "% of height, dark " + (dark * 100f).ToString("0") + "%, bright " + (bright * 100f).ToString("0.0") + "%, mean " + mean.ToString("0.000"));
                darkSum += dark; brightSum += bright;
                n++;
            }
            Check("dad is 10–12% of screen height", hMin >= 0.095f && hMax <= 0.125f, "range " + (hMin * 100f).ToString("0.0") + "–" + (hMax * 100f).ToString("0.0") + "%");
            float darkAvg = darkSum / n, brightAvg = brightSum / n;
            Check("histogram: >= 50% deep shadow, >= 3% light islands", darkAvg >= 0.5f && brightAvg >= 0.03f, "dark " + (darkAvg * 100f).ToString("0") + "%, bright " + (brightAvg * 100f).ToString("0.0") + "% (average of " + n + " shots)");

            // Mirar adelante: corriendo a la derecha, el encuadre se adelanta ~1.5 m.
            Teleport(Ground(-54f, 0f));
            yield return 1f;
            var cam = Camera.main.transform;
            float idleOffset = cam.position.x - player.transform.position.x;
            yield return Hold(new Vector2(0.9f, 0f), 2f);
            float runOffset = cam.position.x - player.transform.position.x;
            Stick(Vector2.zero);
            float ahead = runOffset - idleOffset;
            Check("look-ahead in the direction of travel", ahead > 0.9f && ahead < 2.2f, "camera leads by " + ahead.ToString("0.00") + " m");
            yield return 1f;

            // Foco del tilt-shift en el papá.
            float focus = Shader.GetGlobalFloat("_TiltFocusY");
            float dadY = Camera.main.WorldToViewportPoint(player.transform.position + Vector3.up * 0.9f).y;
            Check("tilt-shift focus follows the dad", Mathf.Abs(focus - dadY) < 0.04f, "focus " + focus.ToString("0.00") + ", dad at " + dadY.ToString("0.00"));

            // Amenaza: Lurker en Chase → la cámara se aleja y encuadra entre los dos.
            EnemyController lurker = null;
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (e.name == "Lurker") lurker = e;
            lurker.gameObject.SetActive(true);
            Teleport(Ground(-13f, 0f));
            lurker.GetComponent<NavMeshAgent>().Warp(Ground(-7f, 0f));
            yield return 2f; // que la cámara llegue tras el salto
            float distBefore = Vector3.Distance(cam.position, player.transform.position + Vector3.up);
            lurker.Perception.Suppress(0f);
            player.GetComponent<PlayerHealth>().Restore();
            lurker.AlertTo(player.transform.position);
            float blend = 0f, distThreat = 0f;
            float limit = Time.time + 6f;
            while (Time.time < limit)
            {
                if (lurker.IsHunting) { blend = rig.ThreatBlend; distThreat = Vector3.Distance(cam.position, player.transform.position + Vector3.up); }
                if (blend > 0.95f) break;
                yield return null;
            }
            lurker.ResetAfterPlayerRespawn(player.transform.position, 0f, 9999f);
            Check("threat pulls the camera back", blend > 0.95f && distThreat > distBefore + 1f, "blend " + blend.ToString("0.00") + ", distance " + distBefore.ToString("0.0") + " → " + distThreat.ToString("0.0") + " m");
            lurker.gameObject.SetActive(false);
            player.GetComponent<PlayerHealth>().Restore();
            yield return 1.5f;

            // Lo que tapa al papá se desvanece con dithering (no transparencia).
            ProjectRidge.CameraRig.OcclusionFadeable tree = null;
            foreach (var f in ProjectRidge.CameraRig.OcclusionFadeable.All)
            {
                var p = f.transform.position;
                if (p.x > -50f && p.x < 50f && NavMesh.SamplePosition(p + Vector3.forward * 1.6f, out NavMeshHit hit, 0.4f, NavMesh.AllAreas)) { tree = f; break; }
            }
            float target = 1f;
            string shader = "none";
            if (tree != null)
            {
                Teleport(Ground(tree.transform.position.x, tree.transform.position.z + 1.6f));
                yield return 1.2f;
                target = tree.TargetAlpha;
                shader = tree.Renderers[0].sharedMaterial.shader.name;
                Shot("P3_after_dither");
            }
            Check("occluder fades with dithering", tree != null && target < 1f && shader == "ProjectRidge/DitherFade", (tree != null ? tree.name + " at " + tree.transform.position : "no fadeable") + ", alpha " + target + ", shader " + shader);
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) e.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ P5

        static IEnumerator P5()
        {
            yield return PassTitle();
            Calm();
            EnemyController lurker = null;
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                e.gameObject.SetActive(e.name == "Lurker");
                if (e.name == "Lurker") lurker = e;
            }
            var feedback = lurker.GetComponent<EnemyStateFeedback>();
            var indicators = Object.FindFirstObjectByType<UI.DetectionIndicators>();
            var lanim = lurker.GetComponentInChildren<LurkerAnimator>();

            // Sin cono pintado en el juego normal; solo con la opción de accesibilidad.
            yield return 0.3f;
            bool anyCone = false;
            foreach (var c in Object.FindObjectsByType<VisionCone>(FindObjectsSortMode.None)) anyCone |= c.GetComponent<MeshRenderer>().enabled;
            ProjectRidge.Core.StealthAids.Enabled = true;
            yield return 0.2f;
            bool coneWithAids = false;
            foreach (var c in Object.FindObjectsByType<VisionCone>(FindObjectsSortMode.None)) coneWithAids |= c.GetComponent<MeshRenderer>().enabled;
            ProjectRidge.Core.StealthAids.Enabled = false;
            Check("no ground cone (only as accessibility option)", !anyCone && coneWithAids, "cone visible by default " + anyCone + ", with stealth aids " + coneWithAids);

            // Patrulla: brasa tenue, sin indicador.
            lurker.ResetAfterPlayerRespawn(player.transform.position, 0f, 0f);
            Teleport(Ground(-24f, 0f)); // lejos del Lurker
            yield return 1f;
            Color calmEyes = feedback.EyeColor;
            var calmInd = indicators.State(lurker);
            Shot("P5_after_patrol");

            // De pie a la vista: la sospecha sube (arco que se llena), luego caza (ojos rojos y haz).
            // En terreno abierto (el sendero de la entrada): el Lurker mira hacia el papá y lo va viendo.
            lurker.GetComponent<NavMeshAgent>().Warp(Ground(-52f, 0f));
            Teleport(Ground(-45f, 0f));
            yield return 0.2f;
            lurker.Perception.Suppress(0f);
            lurker.AlertTo(player.transform.position);
            float partialFill = 0f;
            bool sawPartial = false;
            float limit = Time.time + 8f;
            while (Time.time < limit && !lurker.IsHunting)
            {
                var st = indicators.State(lurker);
                if (st.arc && st.fill > 0.1f && st.fill < 0.95f && !sawPartial) { sawPartial = true; partialFill = st.fill; ShotUI("P5_after_detecting"); }
                yield return null;
            }
            yield return 0.4f;
            var huntInd = indicators.State(lurker);
            Color huntEyes = feedback.EyeColor;
            float beam = feedback.Beam;
            ShotUI("P5_after_hunting");
            Check("eyes change with state", calmEyes.r < huntEyes.r * 0.5f && beam > 0.9f, "patrol eyes " + calmEyes + ", hunting " + huntEyes + ", eye beam " + beam.ToString("0.00"));
            Check("detection arc fills, then full when hunted", !calmInd.arc && sawPartial && huntInd.arc && huntInd.fill > 0.99f,
                  "patrol shown " + calmInd.arc + ", partial fill seen " + sawPartial + " (" + partialFill.ToString("0.00") + "), hunting fill " + huntInd.fill.ToString("0.00"));

            // Fuera de cámara: flecha en el borde hacia el Lurker.
            Vector3 away = player.transform.position + (player.transform.position - lurker.transform.position).normalized * 16f;
            Teleport(Ground(away.x, away.z));
            lurker.Perception.Notice(player.transform.position);
            bool arrow = false;
            limit = Time.time + 2f;
            while (Time.time < limit)
            {
                var st = indicators.State(lurker);
                Vector3 vp = Camera.main.WorldToViewportPoint(lurker.transform.position);
                bool off = vp.z < 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f;
                if (off && st.arrow) { arrow = true; ShotUI("P5_after_edge_arrow"); break; }
                yield return null;
            }
            Check("edge arrow when the lurker is off camera", arrow, "arrow shown " + arrow + " (lurker " + lurker.State + ")");

            // La cabeza apunta a lo que vigila.
            lurker.ResetAfterPlayerRespawn(player.transform.position, 0f, 0f);
            lurker.Perception.Suppress(9999f);
            yield return 0.5f;
            Vector3 side = lurker.transform.position + lurker.transform.right * 5f;
            lurker.AlertTo(side);
            float maxYaw = 0f;
            limit = Time.time + 1.2f;
            while (Time.time < limit) { maxYaw = Mathf.Max(maxYaw, Mathf.Abs(lanim.HeadYaw)); yield return null; }
            Check("head turns to what it watches", maxYaw > 20f, "head yaw " + maxYaw.ToString("0") + "° toward a point 90° to the side");
            player.GetComponent<PlayerHealth>().Restore();
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) e.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ P6

        static IEnumerator P6()
        {
            yield return PassTitle();
            Calm();
            var visual = player.transform.Find("Visual");
            var anim = visual.GetComponent<Animator>();
            var smr = visual.GetComponentInChildren<SkinnedMeshRenderer>();

            // Papá nuevo (MakeHuman), mismo controlador y clips, <= 15k triángulos en total.
            int tris = 0;
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                UnityEngine.Mesh m = r is SkinnedMeshRenderer s ? s.sharedMesh : r.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh : null;
                if (m != null && r.gameObject.activeInHierarchy && r.name != "Club" && r.name != "FlareGun" && r.name != "Torch" && r.name != "Backpack") tris += m.triangles.Length / 3;
            }
            string avatarPath = AssetDatabase.GetAssetPath(anim.avatar);
            Check("new dad: MakeHuman body, humanoid, <= 15k tris", avatarPath == MakeHumanImport.Body && anim.avatar.isHuman && tris <= 15000,
                  "avatar " + System.IO.Path.GetFileName(avatarPath) + ", human " + anim.avatar.isHuman + ", body tris " + tris);
            string ctrlPath = AssetDatabase.GetAssetPath(anim.runtimeAnimatorController);
            Check("same animations (Father.controller, UAL clips)", ctrlPath.EndsWith("/Father.controller"), "controller " + ctrlPath);

            // Anima de verdad: caminando, los pies se separan y se cruzan (el retarget funciona).
            Teleport(Ground(-54f, 0f));
            yield return 1f;
            var footL = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            var footR = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            var head = anim.GetBoneTransform(HumanBodyBones.Head);
            float standHead = head.position.y - player.transform.position.y;
            float minGap = 9f, maxGap = -9f;
            Stick(new Vector2(0.55f, 0f));
            float limit = Time.time + 2f;
            while (Time.time < limit)
            {
                Vector3 fwd = player.transform.forward;
                float gap = Vector3.Dot(footL.position - footR.position, fwd);
                minGap = Mathf.Min(minGap, gap); maxGap = Mathf.Max(maxGap, gap);
                yield return null;
            }
            Shot("P6_after");
            Stick(Vector2.zero);
            Check("walk cycle drives the new skeleton", minGap < -0.15f && maxGap > 0.15f && standHead > 1.5f,
                  "left-right foot gap along travel " + minGap.ToString("0.00") + "…" + maxGap.ToString("0.00") + " m, head " + standHead.ToString("0.00") + " m");

            // Materiales: piel con SSS falso, ropa con normales y AO, botas mojadas, pelo recortado; mochila y funda.
            var mats = smr.sharedMaterials;
            Material ByName(string n) => System.Array.Find(mats, x => x.name == n);
            var skin = ByName("Father_Skin"); var cloth = ByName("Father_Clothes"); var boots = ByName("Father_Boots"); var hair = ByName("Father_Hair");
            bool matsOk = skin != null && skin.IsKeywordEnabled("_EMISSION") && cloth != null && cloth.IsKeywordEnabled("_NORMALMAP") && cloth.GetTexture("_OcclusionMap") != null
                          && boots != null && boots.GetFloat("_Smoothness") >= 0.5f && hair != null && hair.IsKeywordEnabled("_ALPHATEST_ON");
            bool props = Find(visual, "Backpack") != null && Find(visual, "ClubSheath") != null;
            Check("materials + backpack with sheathed club", matsOk && props, "materials " + string.Join(",", System.Array.ConvertAll(mats, x => x.name)) + ", backpack+sheath " + props);

            // Lurker: piel oscura y mojada, sin ropa, extremidades >= 15% más largas, cuello largo, ojos pequeños
            // emisivos, silueta asimétrica.
            var lurker = Object.FindFirstObjectByType<EnemyController>();
            var lvis = lurker.transform.Find("Visual");
            var lsmr = System.Array.Find(lvis.GetComponentsInChildren<SkinnedMeshRenderer>(), r => r.name == "SuperHero_Male");
            var baseBody = AssetDatabase.LoadAssetAtPath<GameObject>(QuaterniusImport.Body).transform;
            float Ratio(string bone) => Find(lvis, bone).localPosition.magnitude / Find(baseBody, bone).localPosition.magnitude;
            float arm = (Ratio("lowerarm_l") + Ratio("hand_l")) * 0.5f, armR = (Ratio("lowerarm_r") + Ratio("hand_r")) * 0.5f;
            float leg = (Ratio("calf_l") + Ratio("foot_l")) * 0.5f, neck = Ratio("Head");
            var eyes = System.Array.Find(lvis.GetComponentsInChildren<SkinnedMeshRenderer>(true), r => r.name == "Eyes");
            float eyeSize = eyes.bounds.size.magnitude;
            var lskin = lsmr.sharedMaterial;
            bool lurkerOk = lsmr.sharedMaterials.Length == 1 && lskin.GetFloat("_Smoothness") >= 0.6f && lskin.GetColor("_BaseColor").grayscale < 0.55f
                            && arm >= 1.15f && leg >= 1.15f && neck >= 1.5f && Mathf.Abs(arm - armR) > 0.05f
                            && eyes.sharedMaterial.IsKeywordEnabled("_EMISSION") && eyeSize < 0.25f;
            Check("lurker: wet dark skin, no clothes, long limbs/neck, small glowing eyes, asymmetric", lurkerOk,
                  "materials " + lsmr.sharedMaterials.Length + ", smoothness " + lskin.GetFloat("_Smoothness").ToString("0.00") + ", arm x" + arm.ToString("0.00") + " (right x" + armR.ToString("0.00") + "), leg x" + leg.ToString("0.00") + ", neck x" + neck.ToString("0.00") + ", eyes " + (eyeSize * 100f).ToString("0") + " cm");
            lurker.GetComponent<NavMeshAgent>().Warp(Ground(-50f, 1.5f));
            Teleport(Ground(-53f, -0.5f));
            yield return 1.2f;
            Shot("P6_after_lurker");
        }

        // ------------------------------------------------------------------ P4

        static IEnumerator P4()
        {
            yield return PassTitle();
            Calm();
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) e.gameObject.SetActive(false);
            var vis = player.GetComponent<PlayerVisibility>();

            // Escondites por densidad: ninguno se ve como un círculo (vista cenital + medida de la forma).
            var (report, minIrr, masses) = HollowBuilder.DensityReport();
            Debug.Log("POLISH: P4 density report\n" + report);
            HollowBuilder.DensityImage(ProgressDir + "P4_after_density.png");
            Shots.TopDown(ProgressDir + "P4_after_topdown.png", new Vector3(-12f, 0f, 0f), 30f);
            Check("hiding masses are not circles", masses > 0 && minIrr >= 0.08f, masses + " design masses, least irregular " + minIrr.ToString("0.00") + " (a circle on this grid ~0.03)");

            // Troncos visibles desde la cámara de juego: el tronco bajo la copa que no queda tapado por ella.
            var trunks = new List<string>();
            bool trunksOk = true;
            foreach (var t in Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("HollowFir_")) continue;
                var mr = t.GetComponentInChildren<MeshRenderer>();
                var b = mr.bounds;
                string key = t.name.Split('(')[0];
                if (trunks.Exists(s => s.StartsWith(key))) continue;
                // El tronco libre: de la base a la copa menos lo que la copa tapa a 42° (tan 42° ≈ 0.9 por metro de radio).
                var fir = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Hollow/" + key + ".prefab");
                var mesh = fir.GetComponentInChildren<MeshFilter>().sharedMesh;
                float crownBottom = float.MaxValue, crownRadius = 0f;
                if (mesh.subMeshCount > 1)
                {
                    var tris = mesh.GetTriangles(1);
                    var v = mesh.vertices;
                    foreach (int i in tris) { crownBottom = Mathf.Min(crownBottom, v[i].y); crownRadius = Mathf.Max(crownRadius, new Vector2(v[i].x, v[i].z).magnitude); }
                }
                float free = mesh.subMeshCount > 1 ? crownBottom - Mathf.Tan(42f * Mathf.Deg2Rad) * crownRadius * 0.55f : b.size.y;
                trunks.Add(key + " trunk " + free.ToString("0.0") + " m");
                trunksOk &= free > 0.8f;
            }
            Shot("P4_after_trunks");
            Check("trunks visible from the game camera", trunks.Count >= 4 && trunksOk, string.Join(", ", trunks));

            // Rocas para móvil: <= 3k triángulos.
            int maxTris = 0;
            // En play las rocas se combinan (batching estático): se miran las mallas de los prefabs.
            foreach (var guid in AssetDatabase.FindAssets("PH_Hollow t:Mesh"))
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));
                if (mesh != null && !mesh.name.EndsWith("_Col")) maxTris = Mathf.Max(maxTris, mesh.triangles.Length / 3);
            }
            Check("rocks <= 3k triangles", maxTris > 0 && maxTris <= 3000, "largest rock " + maxTris + " tris");

            // Escondite por densidad: dentro de una masa → oculto; en el borde fino → no.
            var map = AssetDatabase.LoadAssetAtPath<HideDensityMap>("Assets/_Project/Data/HideDensityMap.asset");
            Vector3 center = Ground(-20.6f, -3.3f);
            Vector3 fringe = Vector3.zero;
            for (float r = 0.5f; r < 5f && fringe == Vector3.zero; r += 0.1f)
                for (int a = 0; a < 16; a++)
                {
                    var q = center + new Vector3(Mathf.Cos(a * 0.39f), 0f, Mathf.Sin(a * 0.39f)) * r;
                    float d = map.Sample(q);
                    if (d > 0.18f && d < 0.35f) { fringe = Ground(q.x, q.z); break; }
                }
            Teleport(center);
            yield return 1f;
            bool hiddenIn = vis.IsHidden;
            float dIn = vis.HideDensity;
            Shot("P4_after_hidden");
            Teleport(fringe);
            yield return 1.2f;
            bool hiddenEdge = vis.IsHidden;
            float dEdge = vis.HideDensity;
            Check("hidden by density (mass yes, thin edge no)", hiddenIn && !hiddenEdge && fringe != Vector3.zero,
                  "center density " + dIn.ToString("0.00") + " hidden " + hiddenIn + "; edge " + fringe + " density " + dEdge.ToString("0.00") + " hidden " + hiddenEdge);

            // Ramas secas: pisarlas caminando suma ruido.
            var branch = Object.FindFirstObjectByType<DryBranch>();
            float loudest = 0f;
            System.Action<ProjectRidge.Noise.NoiseEvent> onNoise = e => { if (e.Type == ProjectRidge.Noise.NoiseType.Breakage) loudest = Mathf.Max(loudest, e.Radius); };
            ProjectRidge.Noise.NoiseSystem.NoiseEmitted += onNoise;
            if (branch != null)
            {
                Vector3 bp = branch.transform.position;
                Teleport(Ground(bp.x - 2f, bp.z));
                yield return 0.5f;
                yield return Hold(new Vector2(0.6f, 0f), 2.4f);
                Stick(Vector2.zero);
            }
            ProjectRidge.Noise.NoiseSystem.NoiseEmitted -= onNoise;
            float walkRadius = AssetDatabase.LoadAssetAtPath<ProjectRidge.Noise.NoiseConfig>("Assets/_Project/Data/NoiseConfig.asset").walkStepRadius;
            Check("dry branch cracks louder", branch != null && loudest >= walkRadius + 3.9f, "crack noise " + loudest.ToString("0.0") + " m (walk " + walkRadius + " m)");
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) e.gameObject.SetActive(true);
        }

        // ------------------------------------------------------------------ P2

        static void ShotUI(string name) => Shots.Capture(ProgressDir + name + ".png", player.transform.position, 1600, 900, true);

        /// <summary>Lleva al papá bajo el haz y lo mantiene ahí hasta que la secuencia de captura arranca.</summary>
        static IEnumerator WalkIntoBeam(Searchlight beam, CaptureSequence seq, bool shotExposure)
        {
            bool shot = false;
            float limit = Time.time + 6f;
            while (seq.Current == CaptureSequence.Phase.None && Time.time < limit)
            {
                Teleport(beam.Spot + Vector3.up * 0.1f);
                if (shotExposure && !shot && beam.Exposure > 0.5f) { ShotUI("P2_1_exposure"); shot = true; }
                yield return null;
            }
        }

        static IEnumerator PressKey(Key k)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(k));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        static IEnumerator P2()
        {
            yield return PassTitle();
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) e.gameObject.SetActive(false);
            var seq = Object.FindFirstObjectByType<CaptureSequence>();
            var flow = GameFlow.Instance;
            Searchlight beam = null;
            foreach (var s in Searchlight.Active) if (beam == null || s.Spot.x < beam.Spot.x) beam = s;
            Searchlight.SuppressAll(0f);
            var health = player.GetComponent<PlayerHealth>();

            // ---- Vida 1: el forcejeo falla (pocos toques) → elevación → destello → checkpoint.
            yield return WalkIntoBeam(beam, seq, true);
            Check("capture starts on full exposure", seq.Current != CaptureSequence.Phase.None, "phase " + seq.Current + " beam " + beam.name);
            Vector3 lockSpot = beam.Spot;
            Vector3 lockPos = player.transform.position;
            float beamDrift = 0f, playerDrift = 0f;
            bool narrowed = beam.Radius < beam.Config.spotRadius * 0.95f;
            yield return null;
            ShotUI("P2_2_lock");
            bool shotPar = false;
            while (seq.Current == CaptureSequence.Phase.Lock || seq.Current == CaptureSequence.Phase.Paralysis)
            {
                beamDrift = Mathf.Max(beamDrift, Vector3.Distance(beam.Spot, lockSpot));
                playerDrift = Mathf.Max(playerDrift, Vector3.Distance(Flat(player.transform.position), Flat(lockPos)));
                if (!shotPar && seq.Current == CaptureSequence.Phase.Paralysis && flow != null) { yield return 1f; ShotUI("P2_3_paralysis"); shotPar = true; continue; }
                yield return null;
            }
            Check("lock: beam and dad stop dead, beam narrows and goes cold", beamDrift < 0.01f && playerDrift < 0.05f && narrowed,
                  "beam drift " + beamDrift.ToString("0.000") + " m, dad drift " + playerDrift.ToString("0.000") + " m, narrowed " + narrowed + ", color " + beam.CurrentColor);
            bool sawStruggle = seq.Current == CaptureSequence.Phase.Struggle;
            yield return PressKey(Key.E);
            yield return PressKey(Key.E);
            yield return 0.3f;
            ShotUI("P2_4_struggle_fail");
            float y0 = lockPos.y, maxY = y0;
            bool shotLift = false, sawLift = false;
            float limit = Time.time + 8f;
            while (flow.Current != GameFlow.State.Playing && Time.time < limit)
            {
                if (seq.Current == CaptureSequence.Phase.Lift)
                {
                    sawLift = true;
                    maxY = Mathf.Max(maxY, player.transform.position.y);
                    if (!shotLift && player.transform.position.y > y0 + 1.5f) { ShotUI("P2_5_lift"); shotLift = true; }
                }
                yield return null;
            }
            Check("failed struggle → lift → checkpoint", sawStruggle && sawLift && maxY - y0 > 2.5f && flow.Current == GameFlow.State.Playing && !seq.StruggleUsed,
                  "struggle shown " + sawStruggle + ", lifted " + (maxY - y0).ToString("0.0") + " m, flow " + flow.Current + ", struggle reset " + !seq.StruggleUsed);

            // ---- Vida 2: el forcejeo sale bien → rueda y el haz retrocede ciego.
            yield return 1f;
            Searchlight.SuppressAll(0f);
            yield return WalkIntoBeam(beam, seq, false);
            while (seq.Current != CaptureSequence.Phase.Struggle && seq.Current != CaptureSequence.Phase.None) yield return null;
            lockSpot = beam.Spot;
            Vector3 heldAt = player.transform.position;
            for (int i = 0; i < 6; i++) yield return PressKey(Key.E);
            ShotUI("P2_6_struggle");
            for (int i = 0; i < 8 && seq.Current == CaptureSequence.Phase.Struggle; i++) yield return PressKey(Key.E);
            yield return 0.35f;
            ShotUI("P2_7_freed");
            yield return 0.8f;
            float recoil = Vector3.Distance(Flat(beam.Spot), Flat(lockSpot));
            float fled = Vector3.Distance(Flat(player.transform.position), Flat(heldAt));
            Check("successful struggle → break free, beam recoils blind", flow.Current == GameFlow.State.Playing && !player.InputLocked && recoil > 2f && fled > 0.5f && beam.Exposure == 0f,
                  "flow " + flow.Current + ", beam moved " + recoil.ToString("0.0") + " m, dad moved " + fled.ToString("0.0") + " m, hp " + health.Current);

            // ---- Misma vida, otra captura: sin forcejeo, directo a la elevación.
            yield return 2.5f;
            Searchlight.SuppressAll(0f);
            yield return WalkIntoBeam(beam, seq, false);
            bool struggleAgain = false;
            limit = Time.time + 8f;
            bool flashShot = false;
            while (flow.Current != GameFlow.State.Playing && Time.time < limit)
            {
                struggleAgain |= seq.Current == CaptureSequence.Phase.Struggle;
                if (!flashShot && seq.Current == CaptureSequence.Phase.Lift && player.transform.position.y > y0 + 2.6f) { ShotUI("P2_8_flash"); flashShot = true; }
                yield return null;
            }
            Check("struggle only once per life", !struggleAgain && flow.Current == GameFlow.State.Playing, "struggle offered again " + struggleAgain + ", flow " + flow.Current);
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) e.gameObject.SetActive(true);
        }
    }
}
