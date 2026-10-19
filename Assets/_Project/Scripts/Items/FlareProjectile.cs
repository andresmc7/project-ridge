using ProjectRidge.AI;
using ProjectRidge.Noise;
using UnityEngine;

namespace ProjectRidge.Items
{
    /// <summary>
    /// Bengala disparada: vuela en arco, y al tocar algo arde unos segundos con luz roja (sin sombras) y ruido
    /// periódico que atrae a los enemigos. Si golpea a un enemigo en vuelo, lo aturde.
    /// </summary>
    public class FlareProjectile : MonoBehaviour
    {
        [SerializeField] private CombatConfig config;
        [SerializeField] private Light glow;
        [SerializeField] private ParticleSystem sparks;
        [Tooltip("Lo que detiene la bengala: suelo, rocas, árboles y enemigos.")]
        [SerializeField] private LayerMask hitMask = (1 << 0) | (1 << 10);
        [Min(0.5f)] [SerializeField] private float maxFlightTime = 4f;
        [Tooltip("Opcional: siseo en bucle mientras arde.")]
        [SerializeField] private AudioSource burnAudio;

        private Vector3 velocity;
        private GameObject owner;
        private bool flying;
        private bool burning;
        private float burnEnd;
        private float nextNoise;
        private float flightEnd;

        public bool IsBurning => burning;

        /// <summary>Bengalas ardiendo ahora (para la IA, el audio y el presupuesto de luces).</summary>
        public static readonly System.Collections.Generic.List<FlareProjectile> Burning = new System.Collections.Generic.List<FlareProjectile>();

        public void Launch(Vector3 initialVelocity, GameObject shooter)
        {
            owner = shooter;
            velocity = initialVelocity;
            flying = true;
            flightEnd = Time.time + maxFlightTime;
            SetGlow(0.6f);
        }

        public void Ignite(Vector3 position, GameObject shooter)
        {
            owner = shooter;
            transform.position = position;
            StartBurning();
        }

        private void Update()
        {
            if (flying) Fly(Time.deltaTime);
            else if (burning) Burn();
        }

        private void Fly(float dt)
        {
            velocity.y += config.flareGravity * dt;
            Vector3 step = velocity * dt;
            if (Physics.Raycast(transform.position, step.normalized, out RaycastHit hit, step.magnitude + 0.05f, hitMask, QueryTriggerInteraction.Ignore))
            {
                var enemy = hit.collider.GetComponentInParent<EnemyController>();
                if (enemy != null && !enemy.IsDead)
                {
                    enemy.Interrupt(config.directHitStun, owner != null ? owner.transform.position : transform.position, 2f);
                    if (enemy.TryGetComponent(out EnemyHealth eh)) eh.TakeDamage(config.directHitDamage);
                    transform.position = new Vector3(hit.point.x, enemy.transform.position.y + 0.05f, hit.point.z);
                }
                else
                {
                    transform.position = hit.point + hit.normal * 0.05f;
                }
                StartBurning();
                return;
            }
            transform.position += step;
            if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);
            if (Time.time > flightEnd) StartBurning();
        }

        private void StartBurning()
        {
            flying = false;
            burning = true;
            burnEnd = Time.time + config.burnTime;
            nextNoise = Time.time;
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            SetGlow(1f);
            if (sparks != null) sparks.Play();
            if (burnAudio != null) burnAudio.Play();
            Burning.Add(this);
        }

        private void Burn()
        {
            float remaining = burnEnd - Time.time;
            if (Time.time >= nextNoise)
            {
                nextNoise = Time.time + config.burnNoiseInterval;
                NoiseSystem.Emit(transform.position, config.burnNoiseRadius, NoiseType.Flare, owner);
            }
            // Parpadeo de llama y apagado en el último segundo y medio.
            float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 9f, transform.position.x);
            SetGlow(flicker * Mathf.Clamp01(remaining / 1.5f));
            if (burnAudio != null) burnAudio.volume = Mathf.Clamp01(remaining / 1.5f);
            if (remaining <= 0f) Destroy(gameObject);
        }

        private void SetGlow(float factor)
        {
            if (glow == null) return;
            glow.color = config.lightColor;
            glow.range = config.lightRange;
            glow.intensity = config.lightIntensity * factor;
            glow.enabled = factor > 0.01f;
        }

        private void OnDisable() => Burning.Remove(this);
    }
}
