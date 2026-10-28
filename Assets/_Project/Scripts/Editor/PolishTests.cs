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
        /// <summary>Al terminar, salir de play mode (false para inspeccionar la escena tras la prueba).</summary>
        public static bool StopPlayWhenDone = true;

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
                "P7" => P7(),
                "P8" => P8(),
                "SNEAK" => SneakHintTest(),
                "CPSAFE" => CheckpointSafety(),
                "SKATE" => FootSkate(),
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
            PolishSuite.Report(phase, results.Count - fails, results.Count, string.Join("\n", results.FindAll(r => r.StartsWith("FAIL"))));
            // Sale de play mode al terminar (ver LoopBot.StopPlayWhenDone).
            if (StopPlayWhenDone) EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
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
            player.CancelStandingTurn();
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
            Check("D4 club sheathed while moving", clubOnBack && !combat.ClubInHand, "club parent " + (club != null ? club.parent.name : "none") + ", speed " + runSpeed.ToString("0.00"));

            // D2: giro de 180° en movimiento en ~0.33 s (540°/s), no instantáneo. Parado, el giro grande es en el sitio
            // con pasos (P7, 3.7) y dura más a propósito.
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
            // Más allá de donde estaba el Lurker al lanzar (después camina hacia el ruido de la piedra).
            Vector3 lurkerAtThrow = lurker.transform.position, playerAtThrow = player.transform.position;
            int stonesBefore = inv.Count(stone);
            Tap(Key.Q);
            yield return 1.5f;
            float beyond = Vector3.Distance(Flat(aim), Flat(playerAtThrow)) - Vector3.Distance(Flat(lurkerAtThrow), Flat(playerAtThrow));
            Check("OBJECT: stone to smart target", inv.Count(stone) == stonesBefore - 1 && beyond > 1f, "stones " + stonesBefore + "→" + inv.Count(stone) + ", lands " + beyond.ToString("0.0") + " m beyond the lurker");
            inv.Add(flare, 1);
            Tap(Key.Tab);
            yield return 0.2f;
            bool flareActive = items.Active == flare;
            int flaresBefore = inv.Count(flare); // P0 en la misma sesión pudo dejar una bengala recogida
            Tap(Key.Q);
            yield return 1.5f;
            Check("OBJECT: cycle + flare gun", flareActive && inv.Count(flare) == flaresBefore - 1 && Object.FindObjectsByType<FlareProjectile>(FindObjectsSortMode.None).Length > 0,
                  "flare active " + flareActive + ", flares " + flaresBefore + "→" + inv.Count(flare));
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
            // También la aparición con guion de la sala 1: se enciende al pasar y, a ≤ 14 m, pone al papá en idle tenso (encorvado).
            foreach (var a in Object.FindObjectsByType<ScriptedApparition>(FindObjectsInactive.Include, FindObjectsSortMode.None)) a.gameObject.SetActive(false);
            var rig = Object.FindFirstObjectByType<ProjectRidge.CameraRig.IsoCameraRig>();

            // Escala del personaje y luz: en varias salas, quieto.
            float hMin = 1f, hMax = 0f, darkSum = 0f, brightSum = 0f;
            int n = 0;
            foreach (var s in LookSpots)
            {
                Teleport(Ground(s.x, s.y));
                // La escala se mide con la cámara a su distancia normal: un haz cerca la aleja (ThreatBlend) según la fase
                // del barrido, y junto al reflector S4 nunca vuelve. Haces apagados para medir; encendidos para el histograma.
                var beams = new List<GameObject>();
                foreach (var b in new List<Searchlight>(Searchlight.Active)) { beams.Add(b.gameObject); b.gameObject.SetActive(false); }
                yield return 1.5f;
                // La escala se mide de pie: si el sigilo automático lo agachó (un haz pasando cerca), esperar a que se levante.
                float standBy = Time.time + 4f; // corto: la mirada atrás llega a los 6 s quieto
                while (player.IsCrouching && Time.time < standBy) yield return 0.2f;
                // Ni durante la mirada atrás de P7 (tras 6 s quieto, acumulados entre teletransportes): es una pose baja.
                var anim = player.GetComponentInChildren<FatherAnimator>().GetComponent<Animator>(); // (el Player tiene otro Animator vacío)
                bool lookedAround = false;
                while ((anim.GetCurrentAnimatorStateInfo(0).IsName("LookAround") || player.GetComponentInChildren<FatherAnimator>().Tension > 0.05f || rig.ThreatBlend > 0.05f) && Time.time < standBy) { lookedAround = true; yield return 0.2f; }
                if (lookedAround) yield return 0.6f; // fin de la transición de vuelta al idle
                float h = DadScreenHeight();
                hMin = Mathf.Min(hMin, h); hMax = Mathf.Max(hMax, h);
                float blendAtMeasure = rig.ThreatBlend;
                foreach (var b in beams) b.SetActive(true);
                yield return 0.2f; // unos cuadros para que los haces se dibujen (con 1 s la cámara volvía a alejarse)
                string file = ProgressDir + "P3_after_" + n + ".png";
                Shots.Capture(file, player.transform.position);
                var (dark, bright, mean) = Shots.Histogram(file);
                Debug.Log("POLISH: P3 shot " + n + " at " + s + ": dad " + (h * 100f).ToString("0.0") + "% of height, dark " + (dark * 100f).ToString("0") + "%, bright " + (bright * 100f).ToString("0.0") + "%, mean " + mean.ToString("0.000") + ", clip " + (anim.GetCurrentAnimatorClipInfo(0).Length > 0 ? anim.GetCurrentAnimatorClipInfo(0)[0].clip.name : "-") + ", head " + (anim.GetBoneTransform(HumanBodyBones.Head).position.y - player.transform.position.y).ToString("0.00") + " m, hip " + player.GetComponentInChildren<FatherAnimator>().HipOffset.ToString("0.00") + " m, tension " + player.GetComponentInChildren<FatherAnimator>().Tension.ToString("0.00") + ", threat blend at measure " + blendAtMeasure.ToString("0.00"));
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
            // Línea base con la cámara quieta: tras el salto desde la última toma, el amortiguado tarda en llegar.
            float idleOffset = cam.position.x - player.transform.position.x, settleUntil = Time.time + 4f;
            for (float prev = float.MaxValue; Mathf.Abs(idleOffset - prev) > 0.005f && Time.time < settleUntil; )
            { prev = idleOffset; yield return 0.2f; idleOffset = cam.position.x - player.transform.position.x; }
            yield return Hold(new Vector2(0.9f, 0f), 2f);
            float runOffset = cam.position.x - player.transform.position.x;
            Stick(Vector2.zero);
            float ahead = runOffset - idleOffset;
            Check("look-ahead in the direction of travel", ahead > 0.9f && ahead < 2.2f, "camera leads by " + ahead.ToString("0.00") + " m (idle offset " + idleOffset.ToString("0.00") + ")");
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

        // ------------------------------------------------------------------ P8

        /// <summary>Aviso de sigilo (SneakHint): no sale en sigilo, sale una vez caminando de pie junto a un Lurker.</summary>
        static IEnumerator SneakHintTest()
        {
            yield return PassTitle();
            Calm();
            var hint = player.GetComponent<SneakHint>();
            var toast = GameObject.Find("HUD").transform.Find("SafeArea/SurvivalHud/Toast").GetComponent<UnityEngine.UI.Text>();
            EnemyController lurker = null;
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                if (e.name == "Lurker") lurker = e; else e.gameObject.SetActive(false);
            lurker.Perception.Suppress(9999f);
            // Prado de la sala 3, tramo sin pasto escondite (z = −0.2): Lurker quieto a ~5 m al costado.
            Teleport(Ground(6f, -0.2f));
            lurker.GetComponent<NavMeshAgent>().Warp(Ground(9f, -5f));
            lurker.GetComponent<NavMeshAgent>().isStopped = true;
            yield return 1f;

            // Stick apenas inclinado: sigilo agachado, sin aviso.
            yield return Hold(new Vector2(0.25f, 0f), 2.5f);
            bool crouchedWhileSneaking = player.IsCrouching;
            bool silentWhenSneaking = !hint.Shown;
            Stick(Vector2.zero);
            yield return 1.2f;

            // Caminando de pie: aviso.
            Teleport(Ground(6f, -0.2f));
            yield return 0.8f;
            yield return Hold(new Vector2(0.6f, 0f), 3.5f);
            bool shown = hint.Shown;
            string walkState = "crouched " + player.IsCrouching + " (" + player.GetComponent<AutoStance>().Why + "), mode " + player.Mode + ", speed " + player.CurrentSpeed.ToString("0.0") + ", lurker " + Vector3.Distance(player.transform.position, lurker.transform.position).ToString("0.0") + " m";
            string text = toast.text;
            float alpha = toast.color.a;
            Stick(Vector2.zero);
            Check("sneak hint: none while sneaking, once when walking upright near a Lurker",
                  crouchedWhileSneaking && silentWhenSneaking && shown && alpha > 0.5f && text.Length > 0,
                  "sneak crouched " + crouchedWhileSneaking + ", hint while sneaking " + !silentWhenSneaking + ", walking → shown " + shown + " \"" + text + "\" alpha " + alpha.ToString("0.00") + "; walking: " + walkState);
            yield return 4.5f;
            // Una sola vez por partida.
            Teleport(Ground(6f, -0.2f));
            yield return Hold(new Vector2(0.6f, 0f), 3.5f);
            Stick(Vector2.zero);
            Check("sneak hint shows only once", toast.color.a < 0.1f, "toast alpha " + toast.color.a.ToString("0.00"));
            lurker.GetComponent<NavMeshAgent>().isStopped = false;
        }

        /// <summary>
        /// Checkpoints seguros: reaparecer en cada uno (misma lógica que GameFlow: Lurkers a ≤ 20 m saltan al punto más
        /// lejano de su ruta, gracia de 6 s) y quedarse quieto 30 s. Si un Lurker ve el checkpoint desde su patrulla,
        /// o un haz lo barre, el jugador que espera a mirar la ronda muere en bucle (lo que pasó en la sala 4).
        /// </summary>
        static IEnumerator CheckpointSafety()
        {
            yield return PassTitle();
            const float Watch = 30f;
            var flow = GameFlow.Instance;
            var hc = new SerializedObject(flow).FindProperty("healthConfig").objectReferenceValue as PlayerHealthConfig;
            // Haces incluidos: misma gracia que al reaparecer; cuenta como visto en cuanto el círculo toca al papá.
            var report = new System.Text.StringBuilder();
            int safe = 0, total = 0;
            foreach (Transform cp in GameObject.Find("Checkpoints").transform)
            {
                total++;
                Teleport(cp.position + Vector3.up * 0.05f);
                foreach (var e in EnemyController.Active)
                    e.ResetAfterPlayerRespawn(cp.position, hc.respawnEnemyClearRadius, hc.respawnGraceSeconds);
                Searchlight.ResetAllExposure();
                Searchlight.SuppressAll(hc.respawnGraceSeconds);
                float start = Time.time, detectedAt = -1f;
                string who = "";
                float closest = float.MaxValue;
                while (Time.time - start < Watch && detectedAt < 0f)
                {
                    foreach (var s in Searchlight.Active)
                        if (s.PlayerLit && detectedAt < 0f)
                        {
                            detectedAt = Time.time - start;
                            who = "searchlight " + s.name;
                        }
                    foreach (var e in EnemyController.Active)
                    {
                        if (detectedAt >= 0f) break;
                        float d = Vector3.Distance(e.transform.position, player.transform.position);
                        closest = Mathf.Min(closest, d);
                        var threshold = (new SerializedObject(e).FindProperty("config").objectReferenceValue as EnemyConfig).investigateThreshold;
                        if (e.IsHunting || e.Perception.Suspicion >= threshold)
                        {
                            detectedAt = Time.time - start;
                            who = e.name + " (" + e.State + ") at " + d.ToString("0.0") + " m from " + e.transform.position.ToString("F1") + (player.IsCrouching ? ", dad crouched" : ", dad standing");
                            break;
                        }
                    }
                    yield return 0.25f;
                }
                if (detectedAt < 0f) safe++;
                report.Append(cp.name + ": " + (detectedAt < 0f ? "safe 30 s (closest Lurker " + closest.ToString("0.0") + " m)" : "SEEN after " + detectedAt.ToString("0.0") + " s by " + who) + "; ");
                Debug.Log("POLISH: CPSAFE " + cp.name + " " + (detectedAt < 0f ? "safe" : "seen " + detectedAt.ToString("0.0") + " s " + who));
                // Que lo que lo vio se olvide antes del siguiente checkpoint.
                foreach (var e in EnemyController.Active) e.ResetAfterPlayerRespawn(cp.position, 0f, 0f);
                Searchlight.ResetAllExposure();
            }
            Check("every checkpoint safe for 30 s standing still after respawn", safe == total, safe + "/" + total + " safe — " + report);
        }

        /// <summary>
        /// Pies que patinan (P7, sección 4: "60 s sin animaciones que patinen o floten"): unos 60 s de manejo con el stick
        /// (caminar, trotar, sigilo, arranques, frenadas, giros) midiendo cada cuadro la velocidad horizontal del pie que
        /// está apoyado (a ≤ 3 cm de su altura en reposo). Un pie apoyado que se mueve a más de 0.3 m/s se ve deslizar.
        /// </summary>
        static IEnumerator FootSkate()
        {
            yield return PassTitle();
            Calm();
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) e.gameObject.SetActive(false);
            var anim = player.transform.Find("Visual").GetComponent<Animator>();
            var feet = new[] { anim.GetBoneTransform(HumanBodyBones.LeftFoot), anim.GetBoneTransform(HumanBodyBones.RightFoot) };
            const float PlantMargin = 0.03f, SkateSpeed = 0.3f;

            // Altura de cada pie en reposo (sobre la raíz del papá), en un claro llano de la sala 1.
            Teleport(Ground(-58f, 0f));
            yield return 2f;
            var rest = new float[2];
            for (int f = 0; f < 2; f++) rest[f] = feet[f].position.y - player.transform.position.y;

            var script = new (Vector2 stick, float seconds)[]
            {
                (new Vector2(0.45f, 0f), 4f), (Vector2.zero, 1.5f), (new Vector2(0.9f, 0f), 3f), (Vector2.zero, 1.5f),
                (new Vector2(-0.45f, 0f), 3f), (new Vector2(0f, 0.45f), 2f), (new Vector2(0.45f, 0f), 2f), (Vector2.zero, 1.2f),
                (new Vector2(0.25f, 0f), 4f), (Vector2.zero, 1f), (new Vector2(-0.9f, 0.2f), 3f), (new Vector2(0.9f, -0.2f), 2.5f),
                (Vector2.zero, 1.5f), (new Vector2(0f, -0.6f), 2.5f), (new Vector2(0.6f, 0.6f), 2.5f), (new Vector2(-0.6f, 0.6f), 2f),
                (Vector2.zero, 1.2f), (new Vector2(0.45f, 0f), 3f), (new Vector2(-0.45f, 0f), 1.5f), (new Vector2(0.45f, 0f), 1.5f),
                (Vector2.zero, 1.5f), (new Vector2(1f, 0f), 4f), (new Vector2(-1f, 0f), 2f), (Vector2.zero, 2f),
                (new Vector2(0.3f, 0.3f), 3f), (Vector2.zero, 2f),
            };
            var planted = new Dictionary<string, int>();
            var skating = new Dictionary<string, int>();
            var speedSum = new Dictionary<string, float>();
            var last = new Vector3[2];
            int frame = -1, totalPlanted = 0, totalSkating = 0;
            float driven = 0f, worst = 0f;
            string worstClip = "";
            bool first = true;
            float lastTime = 0f;
            foreach (var (stick, seconds) in script)
            {
                Stick(stick);
                float end = Time.time + seconds;
                while (Time.time < end)
                {
                    yield return 0f;
                    if (Time.frameCount == frame) continue;
                    frame = Time.frameCount;
                    float dt = Time.time - lastTime;
                    lastTime = Time.time;
                    var clips = anim.GetCurrentAnimatorClipInfo(0);
                    string clip = clips.Length > 0 ? clips[0].clip.name : "-";
                    for (int f = 0; f < 2; f++)
                    {
                        Vector3 p = feet[f].position;
                        if (!first && dt > 0f)
                        {
                            float h = p.y - player.transform.position.y;
                            if (h <= rest[f] + PlantMargin)
                            {
                                float v = new Vector2(p.x - last[f].x, p.z - last[f].z).magnitude / dt;
                                planted[clip] = planted.GetValueOrDefault(clip) + 1;
                                speedSum[clip] = speedSum.GetValueOrDefault(clip) + v;
                                totalPlanted++;
                                if (v > SkateSpeed) { skating[clip] = skating.GetValueOrDefault(clip) + 1; totalSkating++; }
                                if (v > worst) { worst = v; worstClip = clip; }
                            }
                        }
                        last[f] = p;
                    }
                    first = false;
                    driven += dt;
                }
            }
            Stick(Vector2.zero);
            var sb = new System.Text.StringBuilder();
            foreach (var kv in planted)
                if (kv.Value >= 10)
                    sb.Append(kv.Key + " " + (100f * skating.GetValueOrDefault(kv.Key) / kv.Value).ToString("0") + "% (mean " + (speedSum[kv.Key] / kv.Value).ToString("0.00") + " m/s, n " + kv.Value + "); ");
            float share = totalPlanted > 0 ? (float)totalSkating / totalPlanted : 1f;
            Debug.Log("POLISH: SKATE per clip — " + sb);
            Check("planted feet do not slide (≤ 10% of planted foot-frames above 0.3 m/s over ~60 s)", share <= 0.1f,
                  (share * 100f).ToString("0.0") + "% skating of " + totalPlanted + " planted foot-frames in " + driven.ToString("0") + " s; worst " + worst.ToString("0.00") + " m/s in " + worstClip + "; rest heights " + rest[0].ToString("0.00") + "/" + rest[1].ToString("0.00") + " m");
        }

        static IEnumerator P8()
        {
            yield return PassTitle();
            Calm();
            var audio = Object.FindFirstObjectByType<AudioDirector>();
            var bank = AssetDatabase.LoadAssetAtPath<AudioBank>("Assets/_Project/Data/AudioBank.asset");
            var lurkers = new List<EnemyController>(Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None));
            var lurker = lurkers.Find(e => e.name == "Lurker") ?? lurkers[0];
            foreach (var e in lurkers) e.Perception.Suppress(9999f);

            // Loudness: el medidor da −20 LUFS con un seno de 997 Hz a −20 dBFS en ambos canales (referencia BS.1770),
            // y cada clip, con su ganancia, queda en el objetivo de su categoría.
            int rate = 48000;
            var sine = new float[rate * 3 * 2];
            for (int i = 0; i < rate * 3; i++) sine[2 * i] = sine[2 * i + 1] = 0.1f * Mathf.Sin(2f * Mathf.PI * 997f * i / rate);
            float reference = MusicBuilder.Integrated(sine, 2, rate);
            var report = new List<string>();
            bool loudOk = Mathf.Abs(reference + 20f) < 0.3f;
            int measured = 0, clamped = 0, limited = 0;
            foreach (var g in bank.gains)
            {
                if (g.clip == null || float.IsNegativeInfinity(g.measuredLufs)) continue;
                measured++;
                float result = g.measuredLufs + 20f * Mathf.Log10(g.gain);
                float target = MusicBuilder.TargetOf(bank, g.clip);
                bool music = target == bank.musicLufs, ambient = target == bank.ambientLufs;
                bool ok = Mathf.Abs(result - target) <= 1f;
                if (!ok && g.peakLimited) limited++;
                else if (!ok) clamped++;
                if (music || ambient) report.Add(g.clip.name + " " + g.measuredLufs.ToString("0.0") + "→" + result.ToString("0.0"));
                if (music || ambient) loudOk &= ok;
            }
            Debug.Log("POLISH: P8 loudness (file → normalized LUFS)\n" + string.Join("\n", report));
            Check("loudness: meter calibrated, music at −20, ambient −24, effects −16 LUFS", loudOk && clamped == 0,
                  "997 Hz at −20 dBFS reads " + reference.ToString("0.00") + " LUFS; " + measured + " clips measured, " + clamped + " off target, " + limited + " transients short of −16 by their −1 dBFS peak");

            // Capas: explorar (lejos de todo), tensión (Lurker a ≤ 12 m), persecución (lo caza). Crossfade de 1.5 s.
            Teleport(Ground(-58f, 0f)); // salida de la cabaña: a 16 m del reflector de la sala 1 (a -45 su haz pasaba a ≤ 10 m según la fase y daba "tensión" en calma)
            foreach (var e in lurkers) if (e != lurker) e.gameObject.SetActive(false);
            lurker.ResetAfterPlayerRespawn(player.transform.position, 0f, 9999f);
            lurker.GetComponent<NavMeshAgent>().Warp(Ground(-24f, 0f));
            yield return bank.layerCrossfade + 0.6f;
            var layer1 = audio.Layer;
            float explore = audio.LayerVolume(AudioDirector.MusicLayer.Explore) / audio.LayerTarget(AudioDirector.MusicLayer.Explore);
            lurker.GetComponent<NavMeshAgent>().Warp(Ground(-49f, 0f));
            yield return bank.layerCrossfade + 0.3f;
            var layer2 = audio.Layer;
            float tension = audio.LayerVolume(AudioDirector.MusicLayer.Tension) / audio.LayerTarget(AudioDirector.MusicLayer.Tension);
            float exploreLeft = audio.LayerVolume(AudioDirector.MusicLayer.Explore) / audio.LayerTarget(AudioDirector.MusicLayer.Explore);
            lurker.ResetAfterPlayerRespawn(player.transform.position, 0f, 0f); // sin gracia: puede cazar
            lurker.GetComponent<NavMeshAgent>().Warp(Ground(-54f, 0f));
            lurker.Perception.Suppress(0f);
            lurker.AlertTo(player.transform.position);
            float limit = Time.time + 8f;
            var hpChase = player.GetComponent<PlayerHealth>();
            while (!lurker.IsHunting && Time.time < limit) yield return null;
            // Que no lo mate mientras se mide (si muere, reaparece en el checkpoint y la música vuelve a explorar).
            limit = Time.time + bank.layerCrossfade + 0.3f;
            while (Time.time < limit) { hpChase.Restore(); yield return null; }
            var layer3 = audio.Layer;
            float chase = audio.LayerVolume(AudioDirector.MusicLayer.Chase) / audio.LayerTarget(AudioDirector.MusicLayer.Chase);
            var sting = audio.LastSting;
            Check("music layers follow the state with a 1.5 s crossfade", layer1 == AudioDirector.MusicLayer.Explore && layer2 == AudioDirector.MusicLayer.Tension && layer3 == AudioDirector.MusicLayer.Chase
                                                                        && explore > 0.95f && tension > 0.95f && exploreLeft < 0.05f && chase > 0.95f,
                  "calm → " + layer1 + " (" + explore.ToString("0.00") + "), lurker at 9 m → " + layer2 + " (" + tension.ToString("0.00") + ", explore left " + exploreLeft.ToString("0.00") + "), hunted → " + layer3 + " (" + chase.ToString("0.00") + ")");
            Check("detected stinger", sting == bank.stingDetected, "last stinger " + (sting != null ? sting.name : "none"));
            lurker.ResetAfterPlayerRespawn(player.transform.position, 0f, 9999f);
            lurker.GetComponent<NavMeshAgent>().Warp(Ground(-55f, 0f));
            foreach (var e in lurkers) { e.gameObject.SetActive(true); e.Perception.Suppress(9999f); }

            // HUD en exploración: vida llena, sin amenaza, 6 s después de entrar → ≤ 4 elementos a la vista.
            var hp = player.GetComponent<PlayerHealth>();
            hp.Restore();
            Teleport(Ground(-24f, 0f));
            GameFlow.Instance.SetObjective("Follow the creek north."); // texto nuevo: SetObjective ignora el mismo texto
            yield return 1f;
            float objectiveShown = GameFlow.Instance.ObjectiveAlpha;
            yield return 5f;
            var visible = VisibleHud();
            float objectiveLater = GameFlow.Instance.ObjectiveAlpha;
            ShotUI("P8_after_explore");
            Check("HUD: ≤ 4 elements visible while exploring, no counters or state text", visible.Count <= 4 && GameObject.Find("StealthHud") == null && GameObject.Find("Resources") == null,
                  visible.Count + " visible: " + string.Join(", ", visible));
            GameFlow.Instance.Pause(true);
            yield return null;
            float objectivePaused = GameFlow.Instance.ObjectiveAlpha;
            GameFlow.Instance.Pause(false);
            Check("objective: short phrase for 4 s, then fades; visible in pause", objectiveShown > 0.95f && objectiveLater < 0.05f && objectivePaused > 0.95f,
                  "\"" + GameFlow.Instance.Objective + "\": at 1 s " + objectiveShown.ToString("0.00") + ", at 6 s " + objectiveLater.ToString("0.00") + ", paused " + objectivePaused.ToString("0.00"));

            // Vida: corazón oculto al 100%, visible al bajar; viñeta roja con poca vida.
            var hud = Object.FindFirstObjectByType<UI.SurvivalHud>();
            float heartFull = hud.HeartAlpha;
            int haptics0 = Haptics.ImpactCount;
            hp.TakeDamage(hp.Max * 0.75f);
            yield return 0.6f;
            float heartHurt = hud.HeartAlpha;
            float vignette = GameObject.Find("LowHealthVignette").GetComponent<CanvasGroup>().alpha;
            ShotUI("P8_after_lowhealth");
            var fa = player.GetComponentInChildren<FatherAnimator>();
            float limp = 0f;
            Stick(new Vector2(0.6f, 0f));
            float until = Time.time + 2f;
            while (Time.time < until) { limp = Mathf.Max(limp, fa.Limp); yield return null; }
            Stick(Vector2.zero);
            Check("health: heart only below 100%, red vignette and limp when low", heartFull < 0.05f && heartHurt > 0.95f && vignette > 0.3f && limp > 0.5f,
                  "heart at full " + heartFull.ToString("0.00") + ", hurt " + heartHurt.ToString("0.00") + ", vignette " + vignette.ToString("0.00") + ", limp " + limp.ToString("0.00"));
            hp.Restore();

            // Recoger: ícono + "+1" junto al botón OBJETO durante 1.2 s.
            var inv = player.GetComponent<PlayerInventory>();
            var stone = ItemsSetupBuilder.LoadItem("Stone");
            while (inv.Count(stone) > 0) inv.TryRemove(stone, 1);
            yield return 1.5f;
            inv.Add(stone, 1);
            yield return 0.2f;
            bool shown = hud.PickupShown;
            ShotUI("P8_after_pickup");
            yield return 1.4f;
            bool gone = !hud.PickupShown;
            Check("pick-up notice: icon + \"+1\" for 1.2 s", shown && gone && stone.icon != null, "shown " + shown + ", gone after 1.6 s " + gone + ", icon " + (stone.icon != null ? stone.icon.name : "none"));

            // Tipografía: una sola fuente (OFL) y ≥ 14 pt en todo el HUD.
            var fonts = new HashSet<string>();
            int minSize = 999;
            foreach (var t in GameObject.Find("HUD").GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                if (t.GetComponentInParent<UI.DebugOverlay>(true) != null) continue; // herramienta de desarrollo, no HUD
                fonts.Add(t.font != null ? t.font.name : "none");
                minSize = Mathf.Min(minSize, t.fontSize);
            }
            int minPt = Mathf.CeilToInt(14f * 2.49f);
            Check("one condensed OFL font, ≥ 14 pt", fonts.Count == 1 && fonts.Contains("BarlowCondensed-SemiBold") && minSize >= minPt,
                  "fonts " + string.Join(",", fonts) + ", smallest " + minSize + " units (14 pt = " + minPt + ")");

            // Hápticos: daño y recogida piden impactos (en el editor solo se cuentan).
            Check("haptics on damage and pick-up", Haptics.ImpactCount - haptics0 >= 2, (Haptics.ImpactCount - haptics0) + " impacts requested");
        }

        /// <summary>Elementos de información del HUD a la vista (los controles táctiles no cuentan, salvo OBJETO con algo).</summary>
        static List<string> VisibleHud()
        {
            var list = new List<string>();
            float Alpha(string name)
            {
                var go = GameObject.Find(name);
                if (go == null || !go.activeInHierarchy) return 0f;
                float a = 1f;
                foreach (var g in go.GetComponentsInParent<CanvasGroup>()) a *= g.alpha;
                if (go.TryGetComponent(out UnityEngine.UI.Text t)) a *= string.IsNullOrEmpty(t.text) ? 0f : t.color.a;
                return a;
            }
            foreach (var n in new[] { "Heart", "Objective", "Prompt", "Toast", "Pickup", "LowHealthVignette", "DamageFlash" })
                if (Alpha(n) > 0.05f) list.Add(n);
            var obj = Object.FindFirstObjectByType<UI.ObjectButton>();
            if (obj != null && obj.GetComponent<CanvasGroup>().alpha > 0.5f) list.Add("ObjectButton");
            var det = Object.FindFirstObjectByType<UI.DetectionIndicators>();
            foreach (var e in EnemyController.Active)
            {
                var st = det.State(e);
                if (st.arc || st.arrow) { list.Add("DetectionIndicator"); break; }
            }
            return list;
        }

        // ------------------------------------------------------------------ P7

        static Animator fatherAnim;
        static readonly HashSet<string> seenStates = new HashSet<string>();
        static readonly string[] P7States = { "Locomotion", "WalkStart", "WalkStop", "RunStop", "Pivot", "TurnLeft", "TurnRight", "LookAround" };

        /// <summary>Anota los estados de la capa base que aparecen (actual y siguiente en una transición).</summary>
        static void NoteStates()
        {
            var cur = fatherAnim.GetCurrentAnimatorStateInfo(0);
            var next = fatherAnim.GetNextAnimatorStateInfo(0);
            foreach (var n in P7States)
                if (cur.IsName(n) || (fatherAnim.IsInTransition(0) && next.IsName(n))) seenStates.Add(n);
        }

        static IEnumerator Watch(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) { NoteStates(); yield return null; }
        }

        /// <summary>Cuadro cercano del papá (cámara propia de lado, sin el HUD) para revisar la animación a 0.25x.</summary>
        static Texture2D CloseFrame(int size)
        {
            var go = new GameObject("P7Cam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 30f;
            Vector3 c = player.transform.position + Vector3.up * 0.95f;
            Vector3 eye = c + Quaternion.Euler(0f, 90f, 0f) * player.transform.forward * 4.2f + Vector3.up * 0.6f;
            go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(c - eye));
            var rt = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(go);
            return tex;
        }

        static void SaveStrip(List<Texture2D> frames, string name)
        {
            int s = frames[0].width;
            var strip = new Texture2D(s * frames.Count, s, TextureFormat.RGB24, false);
            for (int i = 0; i < frames.Count; i++) { strip.SetPixels(i * s, 0, s, s, frames[i].GetPixels()); Object.DestroyImmediate(frames[i]); }
            strip.Apply();
            System.IO.File.WriteAllBytes(ProgressDir + name + ".png", strip.EncodeToPNG());
            Object.DestroyImmediate(strip);
            frames.Clear();
        }

        /// <summary>Altura de la articulación de la punta del pie (ball) sobre el suelo en el idle del papá (medida: 0.7 cm).</summary>
        const float ToeHeight = 0.007f;

        static IEnumerator P7()
        {
            yield return PassTitle();
            Calm();
            foreach (var e in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None)) e.gameObject.SetActive(false);
            fatherAnim = player.transform.Find("Visual").GetComponent<Animator>();
            var fa = fatherAnim.GetComponent<FatherAnimator>();
            var cfg = AssetDatabase.LoadAssetAtPath<FatherAnimationConfig>("Assets/_Project/Data/FatherAnimationConfig.asset");
            float walkTilt = (player.Config.sneakThreshold + player.Config.jogThreshold) * 0.5f;

            // Arranque y frenada caminando (a 0.25x, con tira de cuadros para revisar).
            Teleport(Ground(-54f, 0f));
            yield return 1.5f;
            seenStates.Clear();
            Time.timeScale = 0.25f;
            var frames = new List<Texture2D>();
            Stick(new Vector2(walkTilt, 0f));
            bool driven = false;
            float t0 = Time.time, nextFrame = 0f;
            while (Time.time - t0 < 1.6f)
            {
                NoteStates();
                driven |= player.DrivenSpeed.HasValue;
                if (Time.time - t0 >= nextFrame && frames.Count < 6) { frames.Add(CloseFrame(320)); nextFrame += 0.25f; }
                yield return null;
            }
            SaveStrip(frames, "P7_after_start_025x");
            bool sawStart = seenStates.Contains("WalkStart");
            seenStates.Clear();
            Stick(Vector2.zero);
            Vector3 stopFrom = player.transform.position;
            t0 = Time.time; nextFrame = 0f;
            while (Time.time - t0 < 1.6f)
            {
                NoteStates();
                if (Time.time - t0 >= nextFrame && frames.Count < 6) { frames.Add(CloseFrame(320)); nextFrame += 0.27f; }
                yield return null;
            }
            SaveStrip(frames, "P7_after_stop_025x");
            Time.timeScale = 1f;
            float stopDist = Vector3.Distance(Flat(stopFrom), Flat(player.transform.position));
            Check("walk start and stop clips", sawStart && seenStates.Contains("WalkStop") && driven && player.CurrentSpeed < 0.05f,
                  "start " + sawStart + " (root-motion speed " + driven + "), stop " + seenStates.Contains("WalkStop") + ", stopped in " + stopDist.ToString("0.00") + " m");

            // Frenada en carrera.
            yield return 1f;
            Stick(new Vector2(1f, 0f));
            yield return 2f;
            seenStates.Clear();
            Stick(Vector2.zero);
            yield return Watch(1.2f);
            Check("run stop clip", seenStates.Contains("RunStop"), "states " + string.Join(",", seenStates));

            // Pivote: trotando a la derecha, media vuelta.
            Teleport(Ground(-56f, 0f));
            yield return 1f;
            Stick(new Vector2(1f, 0f));
            yield return 1.5f;
            seenStates.Clear();
            Stick(new Vector2(-1f, 0f));
            yield return Watch(0.8f);
            bool pivot = seenStates.Contains("Pivot");
            string pivotStates = string.Join(",", seenStates);
            Stick(Vector2.zero);
            yield return 1.5f;
            Check("pivot on a 180° reversal while running", pivot, "states " + pivotStates);

            // Giro en el sitio a cada lado: pasos sin avanzar y luego camina.
            var sides = new List<string>();
            string turnLog = "";
            // Sin amenazas cerca: con un haz o la aparición en rango, el sigilo automático lo agacha y lo gira de espaldas a la
            // cobertura mientras espera, y el giro en el sitio (que exige estar de pie) no se pide.
            var muted = new List<GameObject>();
            foreach (var s in new List<Searchlight>(Searchlight.Active)) { muted.Add(s.gameObject); s.gameObject.SetActive(false); }
            foreach (var a in Object.FindObjectsByType<ScriptedApparition>(FindObjectsSortMode.None)) { muted.Add(a.gameObject); a.gameObject.SetActive(false); }
            float maxSlide = 0f;
            foreach (var dir in new[] { new Vector2(0f, walkTilt), new Vector2(0f, -walkTilt) })
            {
                Teleport(Ground(-51f, 0f)); // a 3 m de los postes de farol: junto a uno, AutoStance lo gira de espaldas a la "cobertura"
                player.transform.rotation = Quaternion.LookRotation(Vector3.right);
                player.CancelStandingTurn();
                yield return 1.2f;
                // Que termine lo que venía de la prueba anterior (la frenada tras el pivote seguía sonando en el primer
                // giro y el pedido de giro no entraba): esperar al idle de locomoción.
                float settle = Time.time + 3f;
                while ((!fatherAnim.GetCurrentAnimatorStateInfo(0).IsName("Locomotion") || fatherAnim.IsInTransition(0)) && Time.time < settle) yield return null;
                seenStates.Clear();
                Vector3 from = player.transform.position;
                turnLog += " (at press: speed " + player.CurrentSpeed.ToString("0.00") + ", mult " + player.SpeedMultiplier.ToString("0.00") + ", crouch " + player.IsCrouching + ", yaw " + player.transform.eulerAngles.y.ToString("0") + ")";
                Stick(dir);
                float limit = Time.time + 1.5f;
                while (Time.time < limit)
                {
                    NoteStates();
                    if (player.TurningInPlace) maxSlide = Mathf.Max(maxSlide, Vector3.Distance(Flat(from), Flat(player.transform.position)));
                    yield return null;
                }
                turnLog += (dir.y > 0f ? " [north: " : " [south: ") + string.Join(",", seenStates) + ", yaw " + player.transform.eulerAngles.y.ToString("0") + ", crouch " + player.IsCrouching + "]";
                if (seenStates.Contains("TurnLeft")) sides.Add("left");
                if (seenStates.Contains("TurnRight")) sides.Add("right");
                Stick(Vector2.zero);
                yield return 1f;
            }
            foreach (var go in muted) go.SetActive(true);
            Check("turn in place both ways (steps, no sliding)", sides.Contains("left") && sides.Contains("right") && maxSlide < 0.15f,
                  "sides " + string.Join("+", sides) + ", moved " + maxSlide.ToString("0.00") + " m while turning;" + turnLog);

            // Inclinación en curva: trotando en círculo hacia la derecha.
            Teleport(Ground(-55f, 0f));
            yield return 1f;
            float maxLean = 0f, signed = 0f;
            float a0 = Time.time;
            while (Time.time - a0 < 2.5f)
            {
                float a = (Time.time - a0) * 2.2f;
                Stick(new Vector2(Mathf.Cos(-a), Mathf.Sin(-a)) * 0.95f);
                if (Mathf.Abs(fa.CurveLean) > maxLean) { maxLean = Mathf.Abs(fa.CurveLean); signed = fa.CurveLean; }
                yield return null;
            }
            Stick(Vector2.zero);
            yield return 1.5f;
            Check("leans into curves, max 7°", maxLean > 2f && maxLean <= cfg.maxCurveLean + 0.01f && signed > 0f,
                  "max lean " + signed.ToString("0.0") + "° turning right");

            // IK de pies en el puente: ningún pie flota ni se hunde.
            var bridge = GameObject.Find("WoodenBridge");
            Teleport(bridge.transform.position + Vector3.up * 1.2f);
            yield return 2f;
            // Tobillo a su altura sobre la tabla y punta del pie (ball) apoyada: ni flota ni atraviesa.
            float worst = 0f, worstToe = 0f;
            foreach (var (foot, toe) in new[] { (HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes), (HumanBodyBones.RightFoot, HumanBodyBones.RightToes) })
            {
                var f = fatherAnim.GetBoneTransform(foot);
                var t = fatherAnim.GetBoneTransform(toe);
                if (Physics.Raycast(f.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1.5f, cfg.groundMask, QueryTriggerInteraction.Ignore))
                    worst = Mathf.Max(worst, Mathf.Abs(f.position.y - hit.point.y - cfg.ankleHeight));
                else worst = 9f;
                if (Physics.Raycast(t.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit th, 1.5f, cfg.groundMask, QueryTriggerInteraction.Ignore))
                    worstToe = Mathf.Max(worstToe, Mathf.Abs(t.position.y - th.point.y - ToeHeight));
                else worstToe = 9f;
            }
            Shot("P7_after_bridge");
            Check("foot IK on the bridge", worst < 0.04f && worstToe < 0.05f, "worst ankle error " + (worst * 100f).ToString("0.0") + " cm, worst toe error " + (worstToe * 100f).ToString("0.0")
                  + " cm, hip " + (fa.HipOffset * 100f).ToString("0.0") + " cm, on " + bridge.name);

            // Idle con vida: quieto más de 6 s mira atrás; con un Lurker cerca, idle tenso.
            Teleport(Ground(-54f, 0f));
            seenStates.Clear();
            yield return Watch(cfg.lookAroundAfter + 1.5f);
            bool looked = seenStates.Contains("LookAround");
            var lurker = Object.FindFirstObjectByType<EnemyController>(FindObjectsInactive.Include);
            lurker.gameObject.SetActive(true);
            lurker.Perception.Suppress(9999f);
            lurker.GetComponent<NavMeshAgent>().Warp(Ground(-46f, 2f));
            yield return cfg.tenseBlendTime + 0.6f;
            float tense = fa.Tension;
            Shot("P7_after_tense");
            lurker.gameObject.SetActive(false);
            Check("idle looks around after 6 s; tense with a lurker near", looked && tense > 0.95f, "looked around " + looked + ", tension " + tense.ToString("0.00"));
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
