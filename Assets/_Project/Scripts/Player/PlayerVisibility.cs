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

        public Vector3 ChestPoint => transform.position + Vector3.up * (body.height * 0.7f);
        public Vector3 HeadPoint => transform.position + Vector3.up * (body.height * 0.95f);

        /// <summary>Agachado dentro del pasto alto: invisible para Lurkers y reflectores.</summary>
        public bool IsHidden => config.crouchInGrassHides && player.IsCrouching && tallGrass.IsInTallGrass;

        public float VisibilityMultiplier
        {
            get
            {
                if (IsHidden) return 0f;
                float cover = 1f;
                if (player.IsCrouching) cover *= config.crouchVisibility;
                if (tallGrass.IsInTallGrass) cover *= config.tallGrassVisibility;
                cover = Mathf.Max(cover, config.minVisibility);

                float speed01 = Mathf.Clamp01(player.CurrentSpeed / player.Config.sprintSpeed);
                float movement = Mathf.Lerp(config.stillVisibility, config.sprintVisibility, speed01);
                float light = flashlight != null && flashlight.IsOn ? config.flashlightVisibility : 1f;
                return cover * movement * light;
            }
        }
    }
}
