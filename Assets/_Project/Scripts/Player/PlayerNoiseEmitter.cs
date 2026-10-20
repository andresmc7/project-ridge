using ProjectRidge.Noise;
using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>Emite pasos al NoiseSystem según la distancia real recorrida y el modo de locomoción.</summary>
    public class PlayerNoiseEmitter : MonoBehaviour
    {
        [SerializeField] private NoiseConfig config;
        [SerializeField] private PlayerController player;

        private float distanceSinceStep;

        private void Update()
        {
            if (!player.IsGrounded || player.CurrentSpeed <= 0.01f)
                return;

            distanceSinceStep += player.CurrentSpeed * Time.deltaTime;
            float stride = StrideFor(player.Mode);
            if (distanceSinceStep < stride) return;

            distanceSinceStep -= stride;
            NoiseSystem.Emit(transform.position, RadiusFor(player.Mode), NoiseType.Footstep, gameObject);
        }

        private float StrideFor(MovementMode mode)
        {
            switch (mode)
            {
                case MovementMode.Crouch:
                case MovementMode.Sneak: return config.crouchStride;
                case MovementMode.Jog: return config.jogStride;
                case MovementMode.Sprint: return config.sprintStride;
                default: return config.walkStride;
            }
        }

        private float RadiusFor(MovementMode mode)
        {
            switch (mode)
            {
                case MovementMode.Crouch:
                case MovementMode.Sneak: return config.crouchStepRadius;
                case MovementMode.Jog: return config.jogStepRadius;
                case MovementMode.Sprint: return config.sprintStepRadius;
                default: return config.walkStepRadius;
            }
        }
    }
}
