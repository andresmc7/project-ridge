using System;
using ProjectRidge.Player;
using UnityEngine;

namespace ProjectRidge.AI
{
    /// <summary>
    /// Exposición del papá al reflector del OVNI. Comprobación geométrica (dentro del cono y del alcance) más
    /// raycast desde la luz: rocas, terreno y copas de árboles tapan. Agacharse baja los puntos visibles y la ganancia.
    /// Al llenarse: el OVNI te sigue y alerta a los Lurker cercanos.
    /// </summary>
    public class UFOExposure : MonoBehaviour
    {
        [SerializeField] private UFOConfig config;
        [SerializeField] private UFOController ufo;
        [SerializeField] private PlayerVisibility target;
        [SerializeField] private PlayerController player;
        [Tooltip("Lo que bloquea la luz: Default (rocas, troncos, terreno) y Canopy (copas, triggers).")]
        [SerializeField] private LayerMask occluders = 1;

        private float nextCheck;
        private float lastAlert = -999f;

        public float Exposure { get; private set; }
        public bool IsLit { get; private set; }
        /// <summary>0–1: cuánto falta para que te atrape (solo sube bajo la luz con la exposición llena).</summary>
        public float Capture => captureTimer / config.captureTime;
        public event Action<Vector3> Escalated;
        /// <summary>El OVNI te atrapó: el flujo de juego te devuelve al último checkpoint.</summary>
        public event Action Captured;

        private float captureTimer;

        /// <summary>Tras un respawn: sin exposición acumulada.</summary>
        public void ResetExposure()
        {
            Exposure = 0f;
            captureTimer = 0f;
            IsLit = false;
        }

        private void Update()
        {
            if (Time.time < nextCheck) return;
            float dt = config.checkInterval + Mathf.Max(0f, Time.time - nextCheck);
            nextCheck = Time.time + config.checkInterval;
            dt = Mathf.Min(dt, 0.5f);

            // Agachado solo cuenta el pecho (perfil bajo detrás de cobertura); de pie también la cabeza.
            // En campo abierto el pecho queda iluminado igual: agacharse no anula la luz directa.
            IsLit = Lit(target.ChestPoint) || (!player.IsCrouching && Lit(target.HeadPoint));
            if (IsLit)
            {
                float gain = config.exposureGain * (player.IsCrouching ? config.crouchExposureFactor : 1f);
                Exposure = Mathf.Min(1f, Exposure + gain * dt);
                if (Exposure >= 1f)
                {
                    Escalate();
                    captureTimer += dt;
                    if (captureTimer >= config.captureTime)
                    {
                        ResetExposure();
                        Captured?.Invoke();
                    }
                }
            }
            else
            {
                Exposure = Mathf.Max(0f, Exposure - config.exposureDecay * dt);
                captureTimer = Mathf.Max(0f, captureTimer - dt * 2f);
            }
        }

        private bool Lit(Vector3 point)
        {
            Vector3 origin = ufo.BeamOrigin;
            Vector3 to = point - origin;
            float distance = to.magnitude;
            if (distance > ufo.BeamRange) return false;
            if (Vector3.Angle(ufo.BeamDirection, to) > ufo.BeamAngle * 0.5f * config.detectionConeFraction) return false;
            return !Physics.Linecast(origin, point, occluders, QueryTriggerInteraction.Collide);
        }

        private void Escalate()
        {
            ufo.Track(target.transform.position);
            if (Time.time - lastAlert < config.alertCooldown) return;
            lastAlert = Time.time;
            Vector3 pos = target.transform.position;
            foreach (var enemy in EnemyController.Active)
                if (Vector3.Distance(enemy.transform.position, pos) <= config.alertRadius)
                    enemy.AlertTo(pos);
            Escalated?.Invoke(pos);
        }
    }
}
