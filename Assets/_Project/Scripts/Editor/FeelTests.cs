using System.Collections;
using System.Collections.Generic;
using ProjectRidge.AI;
using ProjectRidge.Core;
using ProjectRidge.Items;
using ProjectRidge.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Pruebas en play mode de las mecánicas de "sensación" y presencia física: pasos, escondido pisado, bengala de
    /// mano, escombros, peso del combate (golpe y zarpazo) y troncos que la IA rodea. Escribe "FEEL: PASS/FAIL ..."
    /// en la consola. Menú: Project Ridge/QA/Run Feel Tests (play mode).
    /// </summary>
    public static class FeelTests
    {
        static readonly List<string> results = new List<string>();
        static IEnumerator routine;
        static float waitUntil;
        static Gamepad pad;
        static Keyboard keyboard;
        static PlayerController player;

        [MenuItem("Project Ridge/QA/Run Feel Tests (play mode)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("FEEL: entra en play mode primero"); return; }
            pad = InputSystem.AddDevice<Gamepad>("FeelPad");
            keyboard = InputSystem.AddDevice<Keyboard>("FeelKeyboard");
            player = Object.FindFirstObjectByType<PlayerController>();
            results.Clear();
            routine = All();
            waitUntil = 0f;
            EditorApplication.update += Tick;
            Debug.Log("FEEL: start");
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
                Debug.LogError("FEEL: exception " + e);
                Finish();
            }
        }

        static void Stop()
        {
            EditorApplication.update -= Tick;
            if (pad != null) { InputSystem.RemoveDevice(pad); pad = null; }
            if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
        }

        static void Finish()
        {
            Stop();
            int fails = 0;
            foreach (var r in results) if (r.StartsWith("FAIL")) fails++;
            Debug.Log("FEEL: DONE " + (results.Count - fails) + "/" + results.Count + " passed\n" + string.Join("\n", results));
        }

        static void Check(string name, bool ok, string detail)
        {
            string line = (ok ? "PASS " : "FAIL ") + name + " — " + detail;
            results.Add(line);
            Debug.Log("FEEL: " + line);
        }

        static void Tap(Key k)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(k));
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }

        static void Stick(Vector2 v, float trigger = 0f)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = v, rightTrigger = trigger });
            InputSystem.Update();
        }

        static void Teleport(Vector3 p)
        {
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = p;
            cc.enabled = true;
        }

        /// <summary>Punto caminable (NavMesh) más cercano: nunca bajo un puente ni en un barranco.</summary>
        static Vector3 Ground(float x, float z)
        {
            if (NavMesh.SamplePosition(new Vector3(x, 1f, z), out NavMeshHit hit, 3f, NavMesh.AllAreas)) return hit.position + Vector3.up * 0.05f;
            var t = Terrain.activeTerrain;
            var p = new Vector3(x, 0f, z);
            p.y = t.SampleHeight(p) + t.transform.position.y + 0.05f;
            return p;
        }

        /// <summary>Estado limpio entre pruebas: vida llena, Lurker de vuelta a su ruta, sin alertas.</summary>
        static void Fresh(EnemyController lurker)
        {
            player.GetComponent<PlayerHealth>().Restore();
            lurker.ResetAfterPlayerRespawn(player.transform.position, 999f, 0f);
        }

        static void SetCrouch(bool on) { if (player.IsCrouching != on) Tap(Key.C); }

        static EnemyController Lurker(string name)
        {
            foreach (var e in EnemyController.Active) if (e.name == name) return e;
            return null;
        }

        /// <summary>Solo deja activo un Lurker y apaga los reflectores (cada prueba aislada).</summary>
        static void Isolate(string keep)
        {
            foreach (var e in EnemyController.Active.ToArray()) if (e.name != keep) e.gameObject.SetActive(false);
            Searchlight.SuppressAll(9999f);
        }

        static IEnumerator All()
        {
            Tap(Key.Space); // pantalla de título
            yield return 1.5f;
            if (GameFlow.Instance != null && GameFlow.Instance.Current != GameFlow.State.Playing) { Tap(Key.Space); yield return 1f; }

            // ---- Bordes: desde el centro de cada sala, correr 5 s hacia el sur y hacia el norte; nunca caer.
            Searchlight.SuppressAll(9999f);
            foreach (var e in EnemyController.Active) e.Perception.Suppress(9999f);
            var centers = new[] { new Vector2(-38f, 0f), new Vector2(-19f, 0f), new Vector2(-8f, 0f), new Vector2(11f, 1f), new Vector2(36f, -1f), new Vector2(61f, 0f) };
            float lowest = 99f;
            string where = "";
            int rescuesBefore = PlayerFallRecovery.Recoveries;
            foreach (var c in centers)
                foreach (var dir in new[] { Vector2.down, Vector2.up, Vector2.left + Vector2.down, Vector2.right + Vector2.up })
                {
                    Teleport(Ground(c.x, c.y));
                    yield return 0.2f;
                    Stick(dir.normalized);
                    yield return 5f;
                    Stick(Vector2.zero);
                    float y = player.transform.position.y;
                    if (y < lowest) { lowest = y; where = c + " dir " + dir; }
                    yield return 0.2f;
                }
            Check("edges hold", lowest > -0.8f && PlayerFallRecovery.Recoveries == rescuesBefore, "lowest y " + lowest.ToString("0.00") + " (" + where + "), rescues " + (PlayerFallRecovery.Recoveries - rescuesBefore));
            foreach (var e in EnemyController.Active) e.Perception.Suppress(0f);

            // ---- Pasos: trotar 3 s en el sendero del refugio, contar pisadas.
            var anim = player.GetComponentInChildren<FatherAnimator>();
            int steps = 0;
            System.Action<bool> onStep = _ => steps++;
            anim.Footstep += onStep;
            Teleport(Ground(-54f, 0f));
            yield return 0.5f;
            Stick(new Vector2(1f, 0f));
            yield return 0.5f;
            int before = steps;
            float t0 = Time.time;
            yield return 3f;
            float cadence = (steps - before) / (Time.time - t0);
            float speed = player.CurrentSpeed;
            Stick(Vector2.zero);
            anim.Footstep -= onStep;
            Check("footsteps", cadence > 2.2f && cadence < 4.2f, "cadence " + cadence.ToString("0.00") + "/s at " + speed.ToString("0.00") + " m/s");
            yield return 0.8f;

            // ---- Escondido y pisado: el Lurker viene a investigar justo al lado del pastizal; al pasarte encima te nota.
            Isolate("Lurker");
            var lurker = Lurker("Lurker");
            var vis = player.GetComponent<PlayerVisibility>();
            Teleport(Ground(-9.4f, -4.4f));
            yield return 0.3f;
            SetCrouch(true);
            yield return 0.6f;
            bool hidden = vis.IsHidden;
            lurker.AlertTo(player.transform.position + Vector3.right * 0.6f);
            float minDist = 99f, maxSusp = 0f;
            EnemyState worst = EnemyState.Patrol;
            float end = Time.time + 40f;
            while (Time.time < end)
            {
                var a = lurker.transform.position; var b = player.transform.position; a.y = b.y = 0f;
                minDist = Mathf.Min(minDist, Vector3.Distance(a, b));
                maxSusp = Mathf.Max(maxSusp, lurker.Perception.Suspicion);
                if (lurker.State == EnemyState.Chase || lurker.State == EnemyState.Attack) { worst = lurker.State; break; }
                yield return null;
            }
            Check("hidden but stepped on", hidden && (worst == EnemyState.Chase || worst == EnemyState.Attack) && minDist < 1.6f, "hidden " + hidden + ", closest " + minDist.ToString("0.0") + " m, suspicion " + maxSusp.ToString("0.00") + ", state " + worst);
            SetCrouch(false);
            Searchlight.SuppressAll(9999f);
            Fresh(lurker);

            // ---- Escondido lejos de la ruta: no debe notarte (control).
            Teleport(Ground(-20.6f, -3.3f));
            yield return 0.3f;
            SetCrouch(true);
            yield return 0.5f;
            maxSusp = 0f;
            end = Time.time + 12f;
            while (Time.time < end) { maxSusp = Mathf.Max(maxSusp, lurker.Perception.Suspicion); yield return null; }
            Check("hidden off-route stays hidden", maxSusp < 0.35f, "suspicion " + maxSusp.ToString("0.00") + " (hidden " + vis.IsHidden + ")");

            // ---- Bengala de mano: dejarla en el pastizal y ver al Lurker ir y quedarse mirándola.
            var flareItem = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Flare.asset");
            player.GetComponent<PlayerInventory>().Add(flareItem, 1);
            SetCrouch(false);
            yield return 0.3f;
            Teleport(Ground(-19.6f, -1.4f));
            yield return 0.3f;
            Tap(Key.Q);
            yield return 1.2f;
            bool burningEarly = HandFlare.Burning.Count > 0;
            var flares = Object.FindObjectsByType<HandFlare>(FindObjectsSortMode.None);
            Vector3 flarePos = flares.Length > 0 ? flares[0].transform.position : Vector3.zero;
            Teleport(Ground(-20.6f, -3.3f)); // a esconderse
            yield return 0.2f;
            SetCrouch(true);
            yield return 2.6f;
            bool burning = HandFlare.Burning.Count > 0;
            EnemyState lureState = EnemyState.Patrol;
            float lureDist = 99f;
            end = Time.time + 12f;
            while (Time.time < end)
            {
                lureState = lurker.State;
                lureDist = Vector3.Distance(lurker.transform.position, flarePos);
                if (lureState == EnemyState.Lured && lureDist < 3.2f) break;
                yield return null;
            }
            Check("hand flare fuse", flares.Length == 1 && !burningEarly && burning, "placed " + flares.Length + ", burning at 1.2 s " + burningEarly + ", at ~4 s " + burning);
            Check("hand flare lures", lureState == EnemyState.Lured && lureDist < 3.2f, "state " + lureState + ", distance to flare " + lureDist.ToString("0.0") + " m");
            yield return 14f; // que se apague
            Check("lure ends with flare", lurker.State != EnemyState.Lured && HandFlare.Burning.Count == 0, "state after burn " + lurker.State);
            SetCrouch(false);

            // ---- Escombros: el Lurker pasa por una rama y la manda volando.
            Fresh(lurker);
            yield return 0.5f;
            KnockableProp target = null;
            float bestD = 99f;
            foreach (var k in Object.FindObjectsByType<KnockableProp>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(k.transform.position, lurker.transform.position);
                if (d < bestD) { bestD = d; target = k; }
            }
            bool knocked = false;
            float flew = 0f;
            if (target != null)
            {
                var start = target.transform.position;
                var agent = lurker.GetComponent<NavMeshAgent>();
                lurker.AlertTo(start);
                end = Time.time + 15f;
                while (Time.time < end)
                {
                    var rb = target.GetComponent<Rigidbody>();
                    if (!rb.isKinematic) knocked = true;
                    flew = Mathf.Max(flew, Vector3.Distance(target.transform.position, start));
                    if (knocked && flew > 0.4f) break;
                    yield return null;
                }
            }
            Check("debris knocked by lurker", knocked && flew > 0.4f, target == null ? "no debris" : "knocked " + knocked + ", moved " + flew.ToString("0.00") + " m");

            // ---- Garrote por la espalda: reacción física, retroceso y embestida del papá.
            Fresh(lurker);
            Teleport(Ground(-20.6f, -3.3f));
            yield return 1f;
            var lp = lurker.transform.position;
            Vector3 behind = lp - lurker.transform.forward * 1.2f;
            Teleport(Ground(behind.x, behind.z));
            player.FaceTowards(lp);
            yield return 0.1f;
            player.GetComponent<PlayerCombat>().RestoreClub(); // el papá empieza sin garrote
            var reaction = lurker.GetComponentInChildren<HitReaction>();
            Vector3 lurkerBefore = lurker.transform.position;
            Vector3 dadBefore = player.transform.position;
            Stick(Vector2.zero, 1f);
            yield return 0.05f;
            Stick(Vector2.zero, 0f);
            float maxBend = 0f;
            end = Time.time + 0.8f;
            while (Time.time < end) { if (reaction != null) maxBend = Mathf.Max(maxBend, reaction.Magnitude); yield return null; }
            float pushed = Vector3.Distance(Flat(lurker.transform.position), Flat(lurkerBefore));
            float lunge = Vector3.Distance(Flat(player.transform.position), Flat(dadBefore));
            Check("club hit has weight", lurker.State == EnemyState.Stunned || pushed > 0.3f, "lurker state " + lurker.State + ", pushed " + pushed.ToString("0.00") + " m, bend " + maxBend.ToString("0") + "°, dad lunge " + lunge.ToString("0.00") + " m");
            Check("club hit reaction visible", maxBend > 8f, "max bend " + maxBend.ToString("0") + "°");

            // ---- Zarpazo: de pie delante del Lurker; debe empujar y doblar al papá.
            yield return 2.5f;
            Searchlight.SuppressAll(9999f);
            var dadReaction = player.GetComponentInChildren<HitReaction>();
            var health = player.GetComponent<PlayerHealth>();
            float hpBefore = health.Current;
            lp = lurker.transform.position;
            Vector3 front = lp + lurker.transform.forward * 1.4f;
            Teleport(Ground(front.x, front.z));
            dadBefore = player.transform.position;
            float dadBend = 0f, dadMoved = 0f;
            end = Time.time + 8f;
            while (Time.time < end)
            {
                if (dadReaction != null) dadBend = Mathf.Max(dadBend, dadReaction.Magnitude);
                if (health.Current < hpBefore) dadMoved = Mathf.Max(dadMoved, Vector3.Distance(Flat(player.transform.position), Flat(dadBefore)));
                if (health.Current < hpBefore && dadBend > 5f && Time.time > end - 7f) { yield return 0.5f; dadMoved = Mathf.Max(dadMoved, Vector3.Distance(Flat(player.transform.position), Flat(dadBefore))); break; }
                yield return null;
            }
            Check("lurker swipe has weight", health.Current < hpBefore && dadBend > 5f && dadMoved > 0.3f, "hp " + hpBefore + "→" + health.Current + ", dad bend " + dadBend.ToString("0") + "°, knocked " + dadMoved.ToString("0.00") + " m");

            // ---- Troncos: el NavMesh no pasa por encima de un tronco de cobertura.
            var log = GameObject.Find("FallenLog");
            bool blocked = false;
            if (log != null)
            {
                var c = log.transform.position;
                blocked = !NavMesh.SamplePosition(c, out NavMeshHit hit, 0.25f, NavMesh.AllAreas);
            }
            Check("cover log blocks navmesh", blocked, log == null ? "no log" : "log at " + log.transform.position);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
