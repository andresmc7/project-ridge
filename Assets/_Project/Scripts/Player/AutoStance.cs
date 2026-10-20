using ProjectRidge.AI;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Agacharse es automático (POLISH_PASS 3.2): el jugador piensa en esconderse y no hacer ruido, nunca en
    /// "agacharse". Decide la postura cada frame:
    /// - dentro de vegetación densa → agachado oculto;
    /// - a ≤ 1 m de una cobertura baja con una amenaza cerca → agachado tras cobertura (de espaldas a ella si quieto);
    /// - joystick apenas inclinado con una amenaza cerca → caminar sigiloso agachado;
    /// - trotar o correr → de pie, siempre.
    /// Para levantarse hacen falta unos segundos seguidos fuera de esas condiciones (histéresis).
    /// </summary>
    public class AutoStance : MonoBehaviour
    {
        public enum Reason { None, Vegetation, Cover, Sneak, Forced }

        [SerializeField] private StealthConfig config;
        [SerializeField] private PlayerController player;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private PlayerVisibility visibility;

        private readonly Collider[] overlap = new Collider[16];
        private float clearSince;
        private bool forced;

        /// <summary>Quiere estar agachado (lo lee PlayerController).</summary>
        public bool WantsCrouch { get; private set; }
        public Reason Why { get; private set; }
        /// <summary>Hay un Lurker despierto o un haz del OVNI cerca.</summary>
        public bool ThreatNear { get; private set; }
        /// <summary>Dirección hacia la cobertura baja más cercana (cero si no hay).</summary>
        public Vector3 CoverDirection { get; private set; }

        private void OnEnable() => input.CrouchPressed += OnForce;
        private void OnDisable() => input.CrouchPressed -= OnForce;

        /// <summary>C (solo pruebas): fuerza el agachado hasta volver a pulsarla.</summary>
        private void OnForce() => forced = !forced;

        private void Update()
        {
            ThreatNear = FindThreat();
            CoverDirection = FindCover();
            float tilt = Mathf.Clamp01(input.Move.magnitude);
            bool running = tilt >= player.Config.jogThreshold || input.SprintRequested;
            bool fast = running || player.Mode == MovementMode.Jog || player.Mode == MovementMode.Sprint
                        || (tilt >= player.Config.sneakThreshold && !player.IsCrouching);

            Reason want = Reason.None;
            if (forced) want = Reason.Forced;
            else if (visibility.HideDensity >= config.hideDensity && !running) want = Reason.Vegetation;
            else if (!fast && ThreatNear && CoverDirection != Vector3.zero) want = Reason.Cover;
            else if (ThreatNear && tilt >= player.Config.stickDeadzone && tilt < player.Config.sneakThreshold) want = Reason.Sneak;

            if (want != Reason.None)
            {
                WantsCrouch = true;
                Why = want;
                clearSince = -1f;
            }
            else if (WantsCrouch)
            {
                // Correr levanta en el acto; lo demás espera la histéresis.
                if (clearSince < 0f) clearSince = Time.time;
                if (running || Time.time - clearSince >= config.standUpDelay)
                {
                    WantsCrouch = false;
                    Why = Reason.None;
                }
            }

            // Quieto tras la cobertura: de espaldas a ella (asomarse por encima es leer la escena).
            if (WantsCrouch && Why == Reason.Cover && tilt < player.Config.stickDeadzone && !player.ActionLocked)
            {
                Quaternion away = Quaternion.LookRotation(-CoverDirection, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, away, config.coverTurnSpeed * Time.deltaTime);
            }
        }

        private bool FindThreat()
        {
            Vector3 p = transform.position;
            float r2 = config.threatRadius * config.threatRadius;
            foreach (var e in EnemyController.Active)
                if (!e.IsDead && e.State != EnemyState.Stunned && (e.transform.position - p).sqrMagnitude <= r2) return true;
            float b2 = config.beamThreatRadius * config.beamThreatRadius;
            foreach (var s in Searchlight.Active)
            {
                Vector3 d = s.Spot - p;
                d.y = 0f;
                if (d.sqrMagnitude <= b2) return true;
            }
            return false;
        }

        private Vector3 FindCover()
        {
            Vector3 p = transform.position;
            int n = Physics.OverlapSphereNonAlloc(p + Vector3.up * 0.5f, config.coverRadius + 0.6f, overlap, config.coverMask, QueryTriggerInteraction.Ignore);
            Vector3 best = Vector3.zero;
            float bestD = config.coverRadius;
            for (int i = 0; i < n; i++)
            {
                var c = overlap[i];
                if (c is TerrainCollider || c.attachedRigidbody != null) continue;
                var b = c.bounds;
                float h = b.max.y - p.y;
                if (h < config.coverMinHeight || h > config.coverMaxHeight) continue;
                Vector3 closest = c.ClosestPoint(p + Vector3.up * 0.4f);
                Vector3 to = closest - p;
                to.y = 0f;
                float d = to.magnitude;
                if (d < bestD && d > 0.01f) { bestD = d; best = to / d; }
            }
            return best;
        }
    }
}
