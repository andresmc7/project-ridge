using System;
using System.Collections.Generic;
using ProjectRidge.Core;
using ProjectRidge.Player;
using UnityEngine;

namespace ProjectRidge.AI
{
    /// <summary>
    /// Reflector rojo de la nave. El círculo recorre una ruta fija (lazo o ida y vuelta) con pausas en cada punto;
    /// la posición depende solo del reloj, así que el patrón es exacto y se puede aprender. Bajo la luz y sin
    /// esconderse (agachado en el pasto) la exposición se llena y la nave se lleva al papá al último checkpoint.
    /// </summary>
    public class Searchlight : MonoBehaviour
    {
        [SerializeField] private SearchlightConfig config;
        [Tooltip("Puntos de la ruta sobre el suelo, en mundo.")]
        [SerializeField] private Vector3[] route;
        [Tooltip("true: lazo cerrado. false: ida y vuelta.")]
        [SerializeField] private bool loop = true;
        [Min(0.1f)] [SerializeField] private float speed = 2.4f;
        [Min(0f)] [SerializeField] private float pauseAtPoints = 0.6f;
        [Tooltip("Desfase del reloj (s): dos reflectores con la misma ruta no van juntos.")]
        [SerializeField] private float phase;
        [Tooltip("Inactivo hasta que algo lo encienda (p. ej. el clímax de la radio).")]
        [SerializeField] private bool startActive = true;
        [SerializeField] private Renderer spotVolume;
        [SerializeField] private Renderer beamVisual;

        public static readonly List<Searchlight> Active = new List<Searchlight>();
        /// <summary>Empezó a llenarse la exposición (para audio y HUD).</summary>
        public static event Action<Searchlight> PlayerSpotted;
        /// <summary>Exposición llena: la nave se lleva al papá.</summary>
        public static event Action<Searchlight> PlayerCaught;

        public Vector3 Spot { get; private set; }
        public Vector3 Source => Spot + new Vector3(0f, config.beamHeight, config.beamBackOffset);
        public float Radius => config.spotRadius;
        public float Exposure { get; private set; }
        public bool PlayerLit { get; private set; }
        public SearchlightConfig Config => config;
        /// <summary>Multiplicador del reloj (el clímax acelera los barridos).</summary>
        public float SpeedScale { get; set; } = 1f;

        private static PlayerVisibility player;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int AlarmId = Shader.PropertyToID("_Alarm");

        private float clock;
        private float cycle;
        private Vector3[] sequence;
        private float[] moveTimes;
        private Vector3? overrideTarget;
        private MaterialPropertyBlock block;

        public void Activate(bool on)
        {
            if (on == enabled && gameObject.activeSelf) return;
            gameObject.SetActive(true);
            enabled = on;
            if (spotVolume != null) spotVolume.enabled = on;
            if (beamVisual != null) beamVisual.enabled = on;
        }

        /// <summary>Lleva el círculo a un punto fijo y lo deja ahí (final). null vuelve a la ruta.</summary>
        public void Override(Vector3? target) => overrideTarget = target;

        private static float suppressedUntil;

        /// <summary>Ningún reflector detecta durante unos segundos (gracia tras reaparecer).</summary>
        public static void SuppressAll(float seconds)
        {
            suppressedUntil = Time.time + seconds;
            ResetAllExposure();
        }

        public static void ResetAllExposure()
        {
            foreach (var s in Active) { s.Exposure = 0f; s.PlayerLit = false; }
        }

        private void Awake()
        {
            BuildTimeline();
            clock = phase;
            Spot = Evaluate(clock);
            if (!startActive) Activate(false);
        }

        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() { Active.Remove(this); PlayerLit = false; Exposure = 0f; }

        private void BuildTimeline()
        {
            if (route == null || route.Length == 0) { sequence = new[] { transform.position }; moveTimes = new[] { 0f }; cycle = 1f; return; }
            var seq = new List<Vector3>(route);
            if (!loop) for (int i = route.Length - 2; i >= 1; i--) seq.Add(route[i]);
            sequence = seq.ToArray();
            moveTimes = new float[sequence.Length];
            cycle = 0f;
            for (int i = 0; i < sequence.Length; i++)
            {
                Vector3 a = sequence[i], b = sequence[(i + 1) % sequence.Length];
                moveTimes[i] = Vector3.Distance(a, b) / speed;
                cycle += pauseAtPoints + moveTimes[i];
            }
            cycle = Mathf.Max(cycle, 0.01f);
        }

        private Vector3 Evaluate(float t)
        {
            if (sequence.Length == 1) return sequence[0];
            t = Mathf.Repeat(t, cycle);
            for (int i = 0; i < sequence.Length; i++)
            {
                if (t < pauseAtPoints) return sequence[i];
                t -= pauseAtPoints;
                if (t < moveTimes[i])
                {
                    float k = moveTimes[i] > 0f ? t / moveTimes[i] : 1f;
                    // Arranque y frenada suaves: se lee como una máquina que busca, no como un robot.
                    k = Mathf.SmoothStep(0f, 1f, k);
                    return Vector3.Lerp(sequence[i], sequence[(i + 1) % sequence.Length], k);
                }
                t -= moveTimes[i];
            }
            return sequence[0];
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            clock += dt * SpeedScale;
            Vector3 target = overrideTarget ?? Evaluate(clock);
            Spot = overrideTarget.HasValue ? Vector3.MoveTowards(Spot, target, speed * 2f * dt) : target;

            Detect(dt);
            UpdateVisuals();
        }

        private void Detect(float dt)
        {
            var flow = GameFlow.Instance;
            bool playing = flow == null || flow.Current == GameFlow.State.Playing;
            if (player == null) player = FindFirstObjectByType<PlayerVisibility>();
            if (!playing || player == null || overrideTarget.HasValue || Time.time < suppressedUntil)
            {
                PlayerLit = false;
                Exposure = Mathf.Max(0f, Exposure - config.exposureDecay * dt);
                return;
            }

            Vector3 p = player.transform.position;
            Vector2 d = new Vector2(p.x - Spot.x, p.z - Spot.z);
            bool inside = d.magnitude <= Radius * config.detectionFraction && Mathf.Abs(p.y - Spot.y) <= config.verticalTolerance;
            PlayerLit = inside && !player.IsHidden;
            if (PlayerLit)
            {
                if (Exposure <= 0f)
                {
                    PlayerSpotted?.Invoke(this);
                    foreach (var enemy in EnemyController.Active)
                        if (Vector3.Distance(enemy.transform.position, p) <= config.alertRadius) enemy.AlertTo(p);
                }
                Exposure = Mathf.Min(1f, Exposure + dt / config.exposeTime);
                if (Exposure >= 1f)
                {
                    ResetAllExposure();
                    PlayerCaught?.Invoke(this);
                }
            }
            else Exposure = Mathf.Max(0f, Exposure - config.exposureDecay * dt);
        }

        private void UpdateVisuals()
        {
            if (block == null) block = new MaterialPropertyBlock();
            // La alarma late mientras se llena: aviso claro de "sal de aquí ya".
            float alarm = Exposure > 0f ? Mathf.Clamp01(Exposure * 1.2f) * (0.75f + 0.25f * Mathf.Sin(Time.time * 30f)) : 0f;
            Color c = Color.Lerp(config.color, config.alarmColor, alarm);

            if (spotVolume != null)
            {
                var t = spotVolume.transform;
                // Volumen bajo: del suelo a la altura del papá, no más (no debe pintar las paredes del acantilado).
                t.SetPositionAndRotation(Spot + Vector3.up * 0.9f, Quaternion.identity);
                t.localScale = new Vector3(Radius * 2f, 3.6f, Radius * 2f);
                spotVolume.GetPropertyBlock(block);
                block.SetColor(ColorId, c);
                block.SetFloat(AlarmId, alarm);
                spotVolume.SetPropertyBlock(block);
            }
            if (beamVisual != null)
            {
                var t = beamVisual.transform;
                Vector3 src = Source;
                Vector3 to = Spot - src;
                float length = to.magnitude;
                t.SetPositionAndRotation(src, Quaternion.LookRotation(to));
                // El cono llega al suelo con el radio del círculo y sigue un poco (el shader lo corta con la profundidad).
                t.localScale = new Vector3(Radius, Radius, length) * 1.25f;
                beamVisual.GetPropertyBlock(block);
                block.SetColor(ColorId, c);
                block.SetFloat(AlarmId, alarm);
                beamVisual.SetPropertyBlock(block);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (route == null) return;
            Gizmos.color = Color.red;
            for (int i = 0; i < route.Length; i++)
            {
                Gizmos.DrawWireSphere(route[i], config != null ? config.spotRadius : 2f);
                if (i > 0 || loop) Gizmos.DrawLine(route[i], route[(i + route.Length - 1) % route.Length]);
            }
        }
    }
}
