using System.Collections.Generic;
using ProjectRidge.AI;
using ProjectRidge.Core;
using ProjectRidge.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Playtest automático del loop de The Hollow (solo en play mode, desde el editor). Juega como debe jugarse:
    /// se agacha en cada pastizal, predice el patrón de los reflectores (exacto) y la orientación de los Lurker,
    /// y avanza al siguiente escondite solo cuando la ventana es segura. Si una sala es imposible, injusta o tiene
    /// la ruta bloqueada, el bot se atasca o muere y lo reporta con la posición. Termina usando la radio y
    /// aguantando el clímax escondido.
    /// </summary>
    public static class LoopBot
    {
        enum Kind { Pass, Hide, Radio, Survive }

        struct Step
        {
            public Kind kind; public Vector3 at; public bool sneak;
            public Step(Kind k, float x, float z, bool s = false) { kind = k; at = new Vector3(x, 0f, z); sneak = s; }
        }

        // Ruta de escondite en escondite (ver HollowLayout.HidePatches). sneak = caminar en vez de trotar (Lurkers oyen).
        static readonly Step[] Route =
        {
            new Step(Kind.Pass, -61.2f, 0.8f), new Step(Kind.Pass, -58.2f, 0.8f), new Step(Kind.Pass, -55f, 0f), new Step(Kind.Pass, -48.5f, 0f),
            new Step(Kind.Hide, -42f, -0.4f), new Step(Kind.Hide, -38f, 1.4f), new Step(Kind.Hide, -34f, -0.8f),
            new Step(Kind.Pass, -29f, 0f), new Step(Kind.Pass, -24f, 0f),
            new Step(Kind.Hide, -20.6f, -3.3f, true), new Step(Kind.Pass, -19.6f, -1f, true), new Step(Kind.Pass, -8.7f, -1f, true), // fin del puente (las barandas llegan a x = -9.1)
            new Step(Kind.Hide, -8.4f, -4.4f, true), new Step(Kind.Pass, -1.5f, 0.2f, true), new Step(Kind.Pass, 0.2f, 0.7f, true),
            new Step(Kind.Hide, 4.2f, 0.6f), new Step(Kind.Hide, 8f, -2.7f), new Step(Kind.Hide, 11.2f, 3.6f), new Step(Kind.Hide, 14.4f, -2.5f),
            new Step(Kind.Hide, 17.8f, 2.8f), new Step(Kind.Hide, 20.6f, 0.4f), new Step(Kind.Pass, 24f, 0f),
            new Step(Kind.Hide, 28.6f, -2.6f, true), new Step(Kind.Pass, 32.4f, 1.2f, true), new Step(Kind.Hide, 36f, 0.7f, true),
            new Step(Kind.Hide, 43f, 1.4f, true), new Step(Kind.Pass, 47.6f, -0.5f, true),
            new Step(Kind.Hide, 52f, -4.6f, true), new Step(Kind.Hide, 57.4f, -3.3f, true), new Step(Kind.Hide, 62.4f, -0.7f, true),
            new Step(Kind.Radio, 65.2f, -0.9f, true), new Step(Kind.Survive, 62.4f, -0.7f, true),
        };

        const int MaxFailures = 8;
        const float Margin = 0.7f, MaxWait = 75f;
        static PlayerMovementConfig Movement => AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>("Assets/_Project/Data/PlayerMovementConfig.asset");
        static float JogSpeed => Movement.jogSpeed;
        static float WalkSpeed => Movement.walkSpeed;

        static int index;
        static bool needResync;
        static float stateSince, lastProgress, startTime;
        static bool moving;
        static Vector3 lastPos;
        static int deaths, captures;
        static Gamepad pad;
        static Keyboard keyboard;
        static PlayerController player;
        static PlayerVisibility visibility;
        static readonly List<string> report = new List<string>();

        [MenuItem("Project Ridge/QA/Run Loop Bot (play mode)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("BOT: entra en play mode primero"); return; }
            pad = InputSystem.AddDevice<Gamepad>("BotPad");
            keyboard = null;
            foreach (var d in InputSystem.devices) if (d.name == "BotKeyboard") keyboard = (Keyboard)d;
            if (keyboard == null) keyboard = InputSystem.AddDevice<Keyboard>("BotKeyboard");
            player = Object.FindFirstObjectByType<PlayerController>();
            visibility = player.GetComponent<PlayerVisibility>();
            index = 0; deaths = 0; captures = 0; moving = false; report.Clear();
            startTime = stateSince = lastProgress = Time.time;
            lastPos = player.transform.position;
            Searchlight.PlayerCaught += OnCaught;
            player.GetComponent<PlayerHealth>().Died += OnDied;
            Tap(Key.Space); // título
            EditorApplication.update += Tick;
            Debug.Log("BOT: start");
        }

        static void OnCaught(Searchlight s)
        {
            captures++;
            Log("CAUGHT by " + s.name + " near step " + index + " at " + player.transform.position);
            if (deaths + captures > MaxFailures) { Finish(false); return; }
            Backtrack();
        }
        static void OnDied()
        {
            deaths++;
            Log("DIED near step " + index + " at " + player.transform.position);
            if (deaths + captures > MaxFailures) { Finish(false); return; } // nunca más un bucle de muertes de horas
            Backtrack();
        }

        /// <summary>Tras morir se reaparece en el último checkpoint: retomar desde el primer paso cercano a él.</summary>
        static void Backtrack()
        {
            moving = false;
            Stick(Vector2.zero);
            stateSince = Time.time + 5.5f; // la secuencia de muerte/captura dura ~5 s
            lastProgress = stateSince;
            needResync = true; // tras reaparecer, retoma desde el paso de la ruta más cercano a donde apareció
        }

        static Vector3 GuessCheckpoint()
        {
            Vector3 best = player.transform.position;
            float bestX = float.MinValue;
            foreach (var cp in Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
                if (cp.transform.position.x <= player.transform.position.x + 0.5f && cp.transform.position.x > bestX) { bestX = cp.transform.position.x; best = cp.transform.position; }
            return bestX > float.MinValue ? best : Route[0].at;
        }

        static void Log(string s)
        {
            string line = "BOT t=" + (Time.time - startTime).ToString("0.0") + " " + s;
            report.Add(line);
            Debug.Log(line);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static void Stick(Vector2 v)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = v });
            InputSystem.Update();
        }

        static void Tap(Key k)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(k));
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }

        static void SetCrouch(bool on)
        {
            if (player.IsCrouching != on) Tap(Key.C);
        }

        static void Finish(bool ok)
        {
            EditorApplication.update -= Tick;
            Searchlight.PlayerCaught -= OnCaught;
            if (player != null) player.GetComponent<PlayerHealth>().Died -= OnDied;
            Stick(Vector2.zero);
            if (pad != null) InputSystem.RemoveDevice(pad);
            Debug.Log("BOT: " + (ok ? "FINISHED" : "FAILED") + " in " + (Time.time - startTime).ToString("0") + " s, deaths " + deaths + ", captures " + captures);
        }

        /// <summary>¿Se puede ir de a hasta b ahora mismo sin pisar luz ni entrar en el cono de un Lurker?</summary>
        static bool LegIsSafe(Vector3 a, Vector3 b, float speed)
        {
            float length = Vector3.Distance(Flat(a), Flat(b));
            float duration = length / speed + 0.4f;
            for (float t = 0f; t <= duration; t += 0.1f)
            {
                Vector3 p = Vector3.Lerp(a, b, Mathf.Clamp01(t / Mathf.Max(0.01f, duration - 0.4f)));
                foreach (var s in Searchlight.Active)
                {
                    Vector3 spot = s.SpotAt(t + 0.15f);
                    if (new Vector2(p.x - spot.x, p.z - spot.z).magnitude < s.Radius * s.Config.detectionFraction + Margin) return false;
                }
                foreach (var e in EnemyController.Active)
                {
                    if (e.IsDead) continue;
                    if (e.State != EnemyState.Patrol) return false;
                    Vector3 to = p - e.transform.position;
                    to.y = 0f;
                    float d = to.magnitude;
                    if (d < e.Config.proximityRadius + 0.8f) return false;
                    if (d < e.Config.viewDistance + 1.5f && Vector3.Angle(e.transform.forward, to) < e.Config.viewAngle * 0.5f + 25f) return false;
                    // Si viene hacia aquí, esperar: en pocos segundos estará más cerca.
                    var agent = e.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (agent != null && agent.hasPath && d < 7f && Vector3.Dot(agent.velocity, -to) > 0.3f) return false;
                }
            }
            return true;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { Finish(false); return; }
            var flow = GameFlow.Instance;
            if (flow != null && flow.Current == GameFlow.State.Ending) { Log("ENDING reached"); Finish(true); return; }
            if (flow != null && flow.Current == GameFlow.State.Clue) { Tap(Key.E); return; }
            if (flow != null && flow.Current != GameFlow.State.Playing) return;
            if (Time.time < stateSince) return;
            if (needResync)
            {
                needResync = false;
                int best = 0;
                float bestD = float.MaxValue;
                for (int i = 0; i <= index && i < Route.Length; i++)
                {
                    float d = Vector3.Distance(Flat(Route[i].at), Flat(player.transform.position));
                    if (d < bestD) { bestD = d; best = i; }
                }
                index = best;
            }
            if (index >= Route.Length) return; // escondido durante la transmisión: esperar el final

            var step = Route[index];
            Vector3 pos = player.transform.position;
            Vector3 to = Flat(step.at) - Flat(pos);

            if (Vector3.Distance(Flat(pos), Flat(lastPos)) > 0.3f) { lastPos = pos; lastProgress = Time.time; }
            if (Time.time - lastProgress > MaxWait) { Log("STUCK at step " + index + " (" + step.kind + " " + step.at + ") pos " + pos); Finish(false); return; }

            if (moving)
            {
                if (to.magnitude < 0.45f)
                {
                    moving = false;
                    Stick(Vector2.zero);
                    stateSince = Time.time + 0.15f;
                    if (step.kind == Kind.Hide || step.kind == Kind.Survive) SetCrouch(true);
                    if (step.kind == Kind.Radio)
                    {
                        Tap(Key.E);
                        Log("RADIO used");
                    }
                    if (step.kind == Kind.Survive)
                    {
                        Log("hiding for the transmission");
                        index++;
                        return;
                    }
                    index++;
                    return;
                }
                float speed = step.sneak ? 0.62f : 1f;
                Vector2 dir = new Vector2(to.x, to.z).normalized * speed;
                Stick(dir);
                return;
            }

            // Escondido (o de paso): esperar una ventana segura para el siguiente tramo.
            float v = step.sneak ? WalkSpeed : JogSpeed;
            if (!LegIsSafe(pos, step.at, v))
            {
                if (index > 0 && Route[index - 1].kind == Kind.Hide) SetCrouch(true);
                return;
            }
            SetCrouch(false);
            moving = true;
        }
    }
}
