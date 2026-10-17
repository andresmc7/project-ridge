using System;
using ProjectRidge.Noise;
using ProjectRidge.Player;
using UnityEngine;

namespace ProjectRidge.AI
{
    /// <summary>
    /// Vista (distancia, campo de visión, línea de visión con raycast) y oído (suscrito al NoiseSystem).
    /// La sospecha sube gradualmente según distancia y visibilidad del jugador; nunca hay detección instantánea
    /// salvo dentro de proximityRadius.
    /// </summary>
    public class EnemyPerception : MonoBehaviour
    {
        [SerializeField] private EnemyConfig config;
        [SerializeField] private NoiseConfig noiseConfig;
        [SerializeField] private Transform eye;
        [SerializeField] private PlayerVisibility target;
        [Tooltip("Lo que tapa la vista y atenúa el sonido (bloques, rocas, terreno). No incluir Player, TallGrass ni Enemy.")]
        [SerializeField] private LayerMask occluders = 1;

        private float nextCheck;

        public float Suspicion { get; private set; }
        public bool CanSeeTarget { get; private set; }
        public bool HasLastKnownPosition { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public PlayerVisibility Target => target;

        /// <summary>Posición aproximada del ruido oído.</summary>
        public event Action<Vector3> NoiseHeard;

        private void OnEnable() => NoiseSystem.NoiseEmitted += OnNoise;
        private void OnDisable() => NoiseSystem.NoiseEmitted -= OnNoise;

        private void Update()
        {
            if (Time.time < nextCheck) return;
            float dt = config.perceptionInterval + (Time.time - nextCheck);
            nextCheck = Time.time + config.perceptionInterval;
            UpdateVision(Mathf.Min(dt, 0.5f));
        }

        public void ForgetTarget() => HasLastKnownPosition = false;

        private void UpdateVision(float dt)
        {
            CanSeeTarget = false;
            if (target == null) return;

            Vector3 eyePos = eye.position;
            Vector3 toTarget = target.ChestPoint - eyePos;
            float distance = toTarget.magnitude;

            bool inRange = distance <= config.viewDistance;
            Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
            bool inCone = Vector3.Angle(transform.forward, flat) <= config.viewAngle * 0.5f;
            bool close = distance <= config.proximityRadius;

            if (inRange && (inCone || close) && HasLineOfSight(eyePos))
            {
                CanSeeTarget = true;
                if (close)
                {
                    Suspicion = 1f;
                }
                else
                {
                    float gain = Mathf.Lerp(config.suspicionGainNear, config.suspicionGainFar, distance / config.viewDistance);
                    Suspicion = Mathf.Min(1f, Suspicion + gain * target.VisibilityMultiplier * dt);
                }

                if (Suspicion >= config.investigateThreshold)
                {
                    LastKnownPosition = target.transform.position;
                    HasLastKnownPosition = true;
                }
                return;
            }

            Suspicion = Mathf.Max(0f, Suspicion - config.suspicionDecay * dt);
        }

        private bool HasLineOfSight(Vector3 eyePos)
        {
            return !Physics.Linecast(eyePos, target.ChestPoint, occluders, QueryTriggerInteraction.Ignore)
                || !Physics.Linecast(eyePos, target.HeadPoint, occluders, QueryTriggerInteraction.Ignore);
        }

        private void OnNoise(NoiseEvent noise)
        {
            if (noise.Source == gameObject) return;

            Vector3 ear = eye.position;
            float distance = Vector3.Distance(ear, noise.Position);
            float radius = noise.Radius * config.hearingSensitivity;
            if (distance > radius) return;

            Vector3 from = noise.Position + Vector3.up * 0.5f;
            if (Physics.Linecast(from, ear, occluders, QueryTriggerInteraction.Ignore))
            {
                radius *= noiseConfig.occludedFactor;
                if (distance > radius) return;
            }

            Vector2 error = UnityEngine.Random.insideUnitCircle * (distance * config.hearingPositionError);
            NoiseHeard?.Invoke(noise.Position + new Vector3(error.x, 0f, error.y));
        }
    }
}
