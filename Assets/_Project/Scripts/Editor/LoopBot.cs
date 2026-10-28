using System.Collections.Generic;
using ProjectRidge.AI;
using ProjectRidge.Core;
using ProjectRidge.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.AI;

namespace ProjectRidge.EditorTools
{
    /// <summary>
    /// Playtest automático del loop de The Hollow (solo en play mode, desde el editor). Juega como debe jugarse y solo
    /// con los controles de POLISH_PASS 3.1 (joystick, ACCIÓN y OBJETO; el agachado es automático): se detiene en cada
    /// pastizal (allí se agacha solo), predice el patrón de los reflectores (exacto) y la orientación de los Lurker,
    /// y avanza al siguiente escondite solo cuando la ventana es segura. Si una sala es imposible, injusta o tiene
    /// la ruta bloqueada, el bot se atasca o muere y lo reporta con la posición. Termina usando la radio y
    /// aguantando el clímax escondido.
    /// </summary>
    public static class LoopBot
    {
        enum Kind { Pass, Hide, Radio, Survive, Take, Throw }

        struct Step
        {
            public Kind kind; public Vector3 at; public bool sneak;
            public Step(Kind k, float x, float z, bool s = false) { kind = k; at = new Vector3(x, 0f, z); sneak = s; }
        }

        // Ruta de escondite en escondite (ver HollowLayout.HidePatches). sneak = caminar en vez de trotar (Lurkers oyen).
        static readonly Step[] Waypoints =
        {
            new Step(Kind.Pass, -61.2f, 0.8f), new Step(Kind.Pass, -58.2f, 0.8f), new Step(Kind.Pass, -55f, 0f), new Step(Kind.Pass, -48.5f, 0f),
            new Step(Kind.Hide, -42f, -0.4f), new Step(Kind.Hide, -38f, 1.4f), new Step(Kind.Hide, -34f, -0.8f),
            new Step(Kind.Pass, -29f, 0f), new Step(Kind.Take, -26.2f, -1.3f), new Step(Kind.Take, -25.5f, 1.1f), new Step(Kind.Throw, -24f, 0f),
            new Step(Kind.Hide, -20.6f, -3.3f, true), new Step(Kind.Pass, -19.6f, -1f, true), new Step(Kind.Pass, -8.7f, -1f, true), // fin del puente (las barandas llegan a x = -9.1)
            new Step(Kind.Hide, -8.4f, -4.4f, true), new Step(Kind.Pass, -1.5f, 0.2f, true), new Step(Kind.Pass, 0.2f, 0.7f, true),
            new Step(Kind.Hide, 4.2f, 0.6f), new Step(Kind.Hide, 8f, -2.7f), new Step(Kind.Hide, 11.2f, 3.6f), new Step(Kind.Hide, 14.4f, -2.5f),
            new Step(Kind.Hide, 17.8f, 2.8f), new Step(Kind.Hide, 20.6f, 0.4f), new Step(Kind.Pass, 24f, 0f),
            new Step(Kind.Hide, 28.6f, -2.6f, true), new Step(Kind.Pass, 32.4f, 1.2f, true), new Step(Kind.Hide, 36f, 0.7f, true),
            new Step(Kind.Hide, 43f, 1.4f, true), new Step(Kind.Pass, 47.6f, -0.5f, true),
            new Step(Kind.Hide, 52f, -4.6f, true), new Step(Kind.Hide, 57.4f, -3.3f, true), new Step(Kind.Hide, 62.4f, -0.7f, true),
            new Step(Kind.Radio, 65.2f, -0.9f, true), new Step(Kind.Survive, 60.3f, -5.7f),
        };

        /// <summary>
        /// Ruta que sigue el bot: los puntos de diseño más las esquinas del NavMesh entre ellos (en línea recta el bot
        /// cortaba esquinas contra los bordes invisibles, p. ej. la entrada al campamento, y se quedaba empujando a la vista).
        /// </summary>
        static readonly List<Step> Route = new List<Step>();

        static void BuildRoute()
        {
            Route.Clear();
            var path = new NavMeshPath();
            for (int i = 0; i < Waypoints.Length; i++)
            {
                var w = Waypoints[i];
                if (i > 0 && NavMesh.SamplePosition(Waypoints[i - 1].at, out var from, 2f, NavMesh.AllAreas)
                          && NavMesh.SamplePosition(w.at, out var to, 2f, NavMesh.AllAreas)
                          && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path))
                    for (int c = 1; c < path.corners.Length - 1; c++)
                        Route.Add(new Step(Kind.Pass, path.corners[c].x, path.corners[c].z, w.sneak));
                Route.Add(w);
            }
        }

        const int MaxFailures = 8;
        const float Margin = 0.7f, MaxWait = 75f;
        /// <summary>Segundos tras llegar hasta quedar agachado y oculto (frenada animada + agacharse solo en la vegetación).</summary>
        const float ArrivalSettle = 0.9f;
        /// <summary>Segundos que se pierden al arrancar desde parado (clip de arranque con avance por root motion).</summary>
        const float StartLatency = 0.6f;
        /// <summary>Márgenes sobre la vista del Lurker (con la patrulla predicha exacta bastan unos pocos).</summary>
        const float ViewDistanceMargin = 1f, ViewAngleMargin = 12f;
        /// <summary>Al terminar, salir de play mode (false para inspeccionar la escena tras la corrida).</summary>
        public static bool StopPlayWhenDone = true;
        static PlayerMovementConfig Movement => AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>("Assets/_Project/Data/PlayerMovementConfig.asset");
        static float JogSpeed => Movement.jogSpeed;
        /// <summary>Inclinación del stick al ir "con cuidado": caminar (no sigiloso, que es demasiado lento para el bot).</summary>
        const float CarefulTilt = 0.45f;
        /// <summary>Trote: por debajo del borde (al borde el joystick fija el sprint, que hace mucho ruido).</summary>
        const float JogTilt = 0.85f;
        static float WalkSpeed => Mathf.Lerp(Movement.walkSpeed, Movement.jogSpeed, Mathf.InverseLerp(Movement.sneakThreshold, Movement.jogThreshold, CarefulTilt));
        static float stopAtX = float.MaxValue;
        static int actions, objects;

        static int index;
        static bool needResync, aimingThrow;
        static int committedUntil = -1;
        /// <summary>Por qué el último tramo evaluado no era seguro (para el informe si se atasca).</summary>
        static string unsafeReason = "";
        /// <summary>Hacia dónde lanzar la piedra de distracción: al norte, fuera de la ruta (si cae delante, el Lurker la busca
        /// justo junto al escondite siguiente).</summary>
        static readonly Vector3 ThrowAim = Vector3.forward;
        const float AimTilt = 0.25f;
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
        public static void Run() => Run(float.MaxValue);

        /// <summary>Corre el bot y termina con éxito al pasar x = stopX (p. ej. el final de la sala 3).</summary>
        public static void Run(float stopX)
        {
            stopAtX = stopX;
            actions = objects = 0;
            if (!EditorApplication.isPlaying) { Debug.LogWarning("BOT: entra en play mode primero"); return; }
            pad = InputSystem.AddDevice<Gamepad>("BotPad");
            keyboard = null;
            foreach (var d in InputSystem.devices) if (d.name == "BotKeyboard") keyboard = (Keyboard)d;
            if (keyboard == null) keyboard = InputSystem.AddDevice<Keyboard>("BotKeyboard");
            player = Object.FindFirstObjectByType<PlayerController>();
            visibility = player.GetComponent<PlayerVisibility>();
            BuildRoute();
            Log("route: " + Waypoints.Length + " design points + " + (Route.Count - Waypoints.Length) + " NavMesh corners");
            index = 0; deaths = 0; captures = 0; moving = false; aimingThrow = false; committedUntil = -1; report.Clear(); lastStates.Clear(); nextStatus = 0f;
            startTime = stateSince = lastProgress = Time.time;
            lastPos = player.transform.position;
            Searchlight.PlayerCaught += OnCaught;
            player.GetComponent<PlayerHealth>().Died += OnDied;
            Tap(Key.Space); // título
            EditorApplication.update += Tick;
            Debug.Log("BOT: start");
        }

        /// <summary>
        /// Igual que Run, pero empieza en el último checkpoint con x ≤ startX (para estudiar una sala sin recorrer
        /// todo el nivel). Retoma la ruta desde el paso más cercano.
        /// </summary>
        public static void RunFrom(float startX, float stopX = float.MaxValue)
        {
            Run(stopX);
            if (player == null) return;
            Transform best = null;
            foreach (var cp in Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
                if (cp.transform.position.x <= startX && (best == null || cp.transform.position.x > best.position.x)) best = cp.transform;
            if (best == null) return;
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.SetPositionAndRotation(best.position, best.rotation);
            body.enabled = true;
            lastPos = player.transform.position;
            index = Route.Count - 1;
            needResync = true;
            Log("starting at checkpoint " + best.name + " " + best.position);
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
            aimingThrow = false;
            committedUntil = -1;
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

        static readonly Dictionary<EnemyController, EnemyState> lastStates = new Dictionary<EnemyController, EnemyState>();
        static float nextStatus;

        /// <summary>Informe: cada cambio de estado de un Lurker (con distancia al papá) y un estado general cada 5 s.</summary>
        static void LogEnemyChanges()
        {
            Vector3 p = player.transform.position;
            foreach (var e in EnemyController.Active)
            {
                if (lastStates.TryGetValue(e, out var s) && s == e.State) continue;
                if (lastStates.ContainsKey(e) || e.State != EnemyState.Patrol)
                    Log(e.name + " -> " + e.State + " at " + e.transform.position + ", " + Vector3.Distance(Flat(e.transform.position), Flat(p)).ToString("0.0") + " m from the player (crouched " + player.IsCrouching + ")");
                lastStates[e] = e.State;
            }
            if (Time.time < nextStatus) return;
            nextStatus = Time.time + 5f;
            Log("status: step " + index + " moving " + moving + " pos " + p);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static void Stick(Vector2 v)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = v });
        }

        static void Tap(Key k)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(k));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }

        static void Finish(bool ok)
        {
            EditorApplication.update -= Tick;
            Searchlight.PlayerCaught -= OnCaught;
            if (player != null) player.GetComponent<PlayerHealth>().Died -= OnDied;
            Stick(Vector2.zero);
            if (pad != null) InputSystem.RemoveDevice(pad);
            Debug.Log("BOT: " + (ok ? "FINISHED" : "FAILED") + " in " + (Time.time - startTime).ToString("0") + " s, deaths " + deaths + ", captures " + captures + ", ACTION presses " + actions + ", OBJECT presses " + objects);
            // Sale de play mode al terminar: editar un .cs con play mode activo recompila a mitad de partida (pasó 3 veces).
            if (StopPlayWhenDone) EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
        }

        /// <summary>¿Se puede ir de a hasta b ahora mismo sin pisar luz ni entrar en el cono de un Lurker?</summary>
        static bool LegIsSafe(Vector3 a, Vector3 b, float speed) => LegIsSafe(a, b, speed, 0f, true, true, out _);

        /// <summary>
        /// Tramo que empieza en t0 (s desde ahora). fromRest: arranca parado (giro en el sitio y clip de arranque);
        /// last: termina ahí (frenada y agacharse). Devuelve la duración del tramo para encadenar el siguiente.
        /// </summary>
        static bool LegIsSafe(Vector3 a, Vector3 b, float speed, float t0, bool fromRest, bool last, out float duration)
        {
            float length = Vector3.Distance(Flat(a), Flat(b));
            // Parado y con un giro grande, el papá gira en el sitio antes de avanzar (P7): se queda en a ese tiempo.
            float turn = 0f;
            float angle = Vector3.Angle(Flat(player.transform.forward), Flat(b) - Flat(a));
            if (fromRest && angle > Movement.standingTurnAngle)
                turn = Mathf.Lerp(Movement.standingTurnTime90, Movement.standingTurnTime180, Mathf.InverseLerp(90f, 180f, angle));
            // Arrancar caminando desde parado usa el clip de arranque (P7): ~1 s hasta la velocidad de crucero.
            float travel = length / speed + (fromRest ? StartLatency : 0f);
            duration = turn + travel + (last ? ArrivalSettle : 0f);
            for (float t = 0f; t <= duration; t += 0.1f)
            {
                Vector3 p = Vector3.Lerp(a, b, Mathf.Clamp01((t - turn) / Mathf.Max(0.01f, travel)));
                foreach (var s in Searchlight.Active)
                {
                    Vector3 spot = s.SpotAt(t0 + t + 0.15f);
                    if (new Vector2(p.x - spot.x, p.z - spot.z).magnitude < s.Radius * s.Config.detectionFraction + Margin) { unsafeReason = "light " + s.name + " at t+" + (t0 + t).ToString("0.0"); return false; }
                }
                foreach (var e in EnemyController.Active)
                {
                    if (e.IsDead) continue;
                    if (e.State != EnemyState.Patrol) { unsafeReason = e.name + " is " + e.State; return false; }
                    // Predicción exacta de la patrulla (puntos, velocidad y pausas: EnemyController.PredictPatrol).
                    e.PredictPatrol(t0 + t, out Vector3 ep, out Vector3 fwd);
                    Vector3 to = p - ep;
                    to.y = 0f;
                    float d = to.magnitude;
                    if (d < e.Config.proximityRadius + 0.8f) { unsafeReason = e.name + " too close at t+" + (t0 + t).ToString("0.0"); return false; }
                    if (d < e.Config.viewDistance + ViewDistanceMargin && Vector3.Angle(fwd, to) < e.Config.viewAngle * 0.5f + ViewAngleMargin) { unsafeReason = e.name + " would see at t+" + (t0 + t).ToString("0.0") + " (" + d.ToString("0.0") + " m)"; return false; }
                }
            }
            return true;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { Finish(false); return; }
            var flow = GameFlow.Instance;
            if (flow != null && flow.Current == GameFlow.State.Ending) { Log("ENDING reached"); Finish(true); return; }
            if (flow != null && flow.Current == GameFlow.State.Clue) { Tap(Key.E); actions++; return; }
            if (player.transform.position.x > stopAtX) { Log("reached x " + stopAtX); Finish(true); return; }
            LogEnemyChanges();
            if (flow != null && flow.Current != GameFlow.State.Playing) return;
            if (Time.time < stateSince) return;
            if (needResync)
            {
                needResync = false;
                int best = 0;
                float bestD = float.MaxValue;
                for (int i = 0; i <= index && i < Route.Count; i++)
                {
                    float d = Vector3.Distance(Flat(Route[i].at), Flat(player.transform.position));
                    if (d < bestD) { bestD = d; best = i; }
                }
                index = best;
            }
            if (index >= Route.Count) return; // escondido durante la transmisión: esperar el final

            var step = Route[index];
            Vector3 pos = player.transform.position;
            Vector3 to = Flat(step.at) - Flat(pos);

            // Lanzar: primero girar (solo con el stick, poco inclinado) hacia la dirección de distracción y luego OBJETO.
            if (aimingThrow)
            {
                if (Vector3.Angle(Flat(player.transform.forward), ThrowAim) > 8f) { Stick(new Vector2(ThrowAim.x, ThrowAim.z) * AimTilt); return; }
                Stick(Vector2.zero);
                aimingThrow = false;
                Tap(Key.Q);
                objects++;
                Log("OBJECT (throw) at " + pos);
                index++;
                stateSince = Time.time + 1.2f;
                return;
            }

            if (Vector3.Distance(Flat(pos), Flat(lastPos)) > 0.3f) { lastPos = pos; lastProgress = Time.time; }
            if (Time.time - lastProgress > MaxWait) { Log("STUCK at step " + index + " (" + step.kind + " " + step.at + ") pos " + pos + " — last reason: " + unsafeReason); Finish(false); return; }

            if (moving)
            {
                // Punto de paso de un tramo ya aprobado: seguir sin frenar (sin giro en el sitio ni frenada a la vista).
                if (to.magnitude < 0.6f && step.kind == Kind.Pass && index < committedUntil)
                {
                    index++;
                    return;
                }
                if (to.magnitude < 0.45f)
                {
                    moving = false;
                    Stick(Vector2.zero);
                    stateSince = Time.time + 0.15f;
                    if (step.kind == Kind.Take) { Tap(Key.E); actions++; Log("ACTION (take) at " + step.at); stateSince = Time.time + 1.6f; }
                    if (step.kind == Kind.Throw) { aimingThrow = true; Log("aiming the stone away from the route (north)"); return; }
                    if (step.kind == Kind.Radio)
                    {
                        Tap(Key.E);
                        actions++;
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
                float speed = step.sneak ? CarefulTilt : JogTilt;
                Vector2 dir = new Vector2(to.x, to.z).normalized * speed;
                Stick(dir);
                return;
            }

            // Tras encender la radio los Lurker vienen a propósito (nunca habrá un tramo "seguro"): al pasto ya.
            if (step.kind == Kind.Survive)
            {
                committedUntil = index;
                moving = true;
                return;
            }

            // Escondido: esperar una ventana segura para todo el tramo hasta el próximo escondite (los puntos "Pass"
            // intermedios están a la vista: no se espera en ellos, se pasa de largo).
            int end = index;
            while (end < Route.Count - 1 && Route[end].kind == Kind.Pass) end++;
            Vector3 from = pos;
            float t0 = 0f;
            for (int i = index; i <= end; i++)
            {
                float v = Route[i].sneak ? WalkSpeed : JogSpeed;
                if (!LegIsSafe(from, Route[i].at, v, t0, i == index, i == end, out float d)) return; // quieto y oculto en el pastizal
                t0 += d;
                from = Route[i].at;
            }
            committedUntil = end;
            moving = true;
        }
    }
}
