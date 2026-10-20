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
