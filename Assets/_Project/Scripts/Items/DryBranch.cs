using System;
using ProjectRidge.Noise;
using ProjectRidge.Player;
using UnityEngine;

namespace ProjectRidge.Items
{
    /// <summary>
    /// Rama seca en el suelo (POLISH_PASS 3.2): pisarla moviéndose la parte con un crujido que suma ruido al paso
    /// (+4 m sobre el radio del modo actual). Es clara y quebradiza a la vista: se puede evitar mirando.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DryBranch : MonoBehaviour
    {
        [SerializeField] private NoiseConfig noise;
        [Tooltip("Metros de ruido que suma el crujido al paso actual.")]
        [Min(0f)] [SerializeField] private float extraRadius = 4f;
        [Tooltip("Velocidad mínima (m/s) para que cruja (quieto encima no suena).")]
        [Min(0f)] [SerializeField] private float minSpeed = 0.2f;
        [Tooltip("Segundos entre crujidos de la misma rama.")]
        [Min(0.1f)] [SerializeField] private float cooldown = 1.5f;

        /// <summary>Crujió en esta posición (audio).</summary>
        public static event Action<Vector3> Cracked;

        private float next;

        private void OnTriggerStay(Collider other)
        {
            if (Time.time < next) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.CurrentSpeed < minSpeed) return;
            next = Time.time + cooldown;
            NoiseSystem.Emit(transform.position, StepRadius(player.Mode) + extraRadius, NoiseType.Breakage, player.gameObject);
            Cracked?.Invoke(transform.position);
        }

        private float StepRadius(MovementMode mode)
        {
            switch (mode)
            {
                case MovementMode.Sprint: return noise.sprintStepRadius;
                case MovementMode.Jog: return noise.jogStepRadius;
                case MovementMode.Walk: return noise.walkStepRadius;
                default: return noise.crouchStepRadius;
            }
        }
    }
}
