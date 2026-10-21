using UnityEngine;

namespace ProjectRidge.AI
{
    /// <summary>
    /// Ojos del Lurker (POLISH_PASS 3.5): la información sale del propio Lurker, no de marcas en el suelo.
    /// Patrulla: brasa tenue. Investigar o buscar: naranja intenso que parpadea. Cazar: rojo, con un haz leve
    /// que sale de los ojos hacia donde mira.
    /// </summary>
    public class EnemyStateFeedback : MonoBehaviour
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private EnemyConfig config;
        [SerializeField] private EnemyController enemy;
        [SerializeField] private EnemyPerception perception;
        [SerializeField] private Renderer[] eyes;
        [Tooltip("Haz leve desde los ojos durante la caza (cono aditivo, opcional).")]
        [SerializeField] private Renderer eyeBeam;
        [Tooltip("Parpadeos por segundo de los ojos al investigar o buscar.")]
        [Min(0f)] [SerializeField] private float flickerRate = 9f;
        [Tooltip("Color del haz de la caza (alfa = intensidad).")]
        [SerializeField] private Color beamColor = new Color(1f, 0.08f, 0.04f, 0.5f);
        [Min(0.01f)] [SerializeField] private float beamFade = 0.25f;

        // Se crea perezosamente: tras un domain reload en play mode Awake no vuelve a correr.
        private MaterialPropertyBlock block;
        private float beam;

        /// <summary>Color actual de los ojos (QA).</summary>
        public Color EyeColor { get; private set; }
        /// <summary>0–1: intensidad del haz de los ojos (QA).</summary>
        public float Beam => beam;

        private void LateUpdate()
        {
            block ??= new MaterialPropertyBlock();
            Color color;
            bool hunting = enemy.State == EnemyState.Chase || enemy.State == EnemyState.Attack;
            switch (enemy.State)
            {
                case EnemyState.Chase:
                case EnemyState.Attack:
                    color = config.eyesHunt;
                    break;
                case EnemyState.Investigate:
                case EnemyState.Search:
                case EnemyState.Lured:
                    // Naranja intenso que tiembla: está atento, todavía no te tiene.
                    float f = 0.65f + 0.35f * Mathf.PerlinNoise(Time.time * flickerRate, enemy.GetInstanceID() * 0.01f);
                    color = config.eyesAlert * f;
                    break;
                case EnemyState.Dead:
                    color = Color.black;
                    break;
                default:
                    color = Color.Lerp(config.eyesCalm, config.eyesAlert, perception.Suspicion * 0.6f);
                    break;
            }
            EyeColor = color;

            foreach (var r in eyes)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionColor, color);
                r.SetPropertyBlock(block);
            }

            beam = Mathf.MoveTowards(beam, hunting ? 1f : 0f, Time.deltaTime / beamFade);
            if (eyeBeam != null)
            {
                eyeBeam.enabled = beam > 0.01f;
                eyeBeam.GetPropertyBlock(block);
                // Haz aditivo: la intensidad va en el color (el alfa no cuenta).
                block.SetColor(ColorId, beamColor * (beamColor.a * beam));
                eyeBeam.SetPropertyBlock(block);
            }
        }
    }
}
