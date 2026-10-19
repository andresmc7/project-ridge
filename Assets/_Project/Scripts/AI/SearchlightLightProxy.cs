using ProjectRidge.Player;
using UnityEngine;

namespace ProjectRidge.AI
{
    /// <summary>
    /// Una sola luz spot real (con sombras) que se pone en el reflector más cercano al papá: los demás círculos son
    /// proyecciones baratas (LightPool). Respeta el presupuesto de luces dinámicas visibles.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class SearchlightLightProxy : MonoBehaviour
    {
        [SerializeField] private SearchlightConfig config;
        [Tooltip("Ángulo extra sobre el que cubre el círculo, para que la luz real abrace el borde brillante.")]
        [Min(1f)] [SerializeField] private float angleMargin = 1.15f;
        [Min(0.1f)] [SerializeField] private float fade = 6f;

        private Light spot;
        private PlayerVisibility player;
        private float weight;

        private void Awake()
        {
            spot = GetComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = config.lightRange;
        }

        private void LateUpdate()
        {
            if (player == null) player = FindFirstObjectByType<PlayerVisibility>();
            Searchlight best = null;
            float bestD = config.proxyMaxDistance;
            if (player != null)
                foreach (var s in Searchlight.Active)
                {
                    Vector3 d = s.Spot - player.transform.position;
                    d.y = 0f;
                    float m = d.magnitude;
                    if (m < bestD) { bestD = m; best = s; }
                }

            float target = best != null ? 1f : 0f;
            weight = Mathf.MoveTowards(weight, target, fade * Time.deltaTime);
            spot.enabled = weight > 0.01f;
            if (best == null) { spot.intensity = config.lightIntensity * weight; return; }

            Vector3 src = best.Source;
            Vector3 to = best.Spot - src;
            transform.SetPositionAndRotation(src, Quaternion.LookRotation(to));
            float half = Mathf.Atan(best.Radius / to.magnitude) * Mathf.Rad2Deg;
            spot.spotAngle = half * 2f * angleMargin;
            spot.innerSpotAngle = half * 1.4f;
            spot.color = Color.Lerp(config.color, config.alarmColor, best.Exposure);
            spot.intensity = config.lightIntensity * weight;
        }
    }
}
