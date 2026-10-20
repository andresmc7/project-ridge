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
            routine = which switch
            {
                "P0" => P0(),
                _ => Unknown(),
            };
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
                if (!routine.MoveNext()) { Finish(); return; }
                if (routine.Current is float s) waitUntil = Time.time + s;
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
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }

        static void Stick(Vector2 v)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = v });
            InputSystem.Update();
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

            // Sin chatarra ni fabricación.
            bool noScrap = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Scrap.asset") == null
                           && GameObject.Find("Pickup_Scrap") == null;
            Check("scrap and crafting removed", noScrap, "scrap asset/pickups gone " + noScrap);
        }
    }
}
