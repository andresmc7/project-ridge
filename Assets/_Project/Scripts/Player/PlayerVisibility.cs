using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>Lo que la percepción enemiga necesita saber del papá: puntos a los que mirar y cuán visible es ahora.</summary>
    public class PlayerVisibility : MonoBehaviour
    {
        [SerializeField] private StealthConfig config;
        [SerializeField] private PlayerController player;
        [SerializeField] private TallGrassDetector tallGrass;
        [SerializeField] private FlashlightController flashlight;
        [SerializeField] private CharacterController body;
        [Tooltip("Mapa de densidad de la vegetación escondite (POLISH_PASS 3.9). Sin mapa, vale el pasto alto (triggers).")]
        [SerializeField] private ProjectRidge.Core.HideDensityMap densityMap;

        public Vector3 ChestPoint => transform.position + Vector3.up * (body.height * 0.7f);
        public Vector3 HeadPoint => transform.position + Vector3.up * (body.height * 0.95f);

        /// <summary>
        /// Densidad de escondite (0–1) en la posición del papá, muestreada del mapa de densidad de la vegetación.
        /// </summary>
        public float HideDensity => densityMap != null ? densityMap.Sample(transform.position) : (tallGrass != null && tallGrass.IsInTallGrass ? 1f : 0f);

        /// <summary>Agachado en vegetación densa: invisible para Lurkers y reflectores.</summary>
        public bool IsHidden => config.crouchInGrassHides && player.IsCrouching && HideDensity >= config.hideDensity;

        public float VisibilityMultiplier
        {
            get
            {
                if (IsHidden) return 0f;
                float cover = 1f;
                if (player.IsCrouching) cover *= config.crouchVisibility;
                if (HideDensity > 0f) cover *= Mathf.Lerp(1f, config.tallGrassVisibility, Mathf.Clamp01(HideDensity / config.hideDensity));
                cover = Mathf.Max(cover, config.minVisibility);

                float speed01 = Mathf.Clamp01(player.CurrentSpeed / player.Config.sprintSpeed);
                float movement = Mathf.Lerp(config.stillVisibility, config.sprintVisibility, speed01);
                float light = flashlight != null && flashlight.IsOn ? config.flashlightVisibility : 1f;
                return cover * movement * light;
            }
        }
    }
}
