using UnityEngine;

namespace ProjectRidge.Player
{
    [CreateAssetMenu(menuName = "Project Ridge/Player Movement Config", fileName = "PlayerMovementConfig")]
    public class PlayerMovementConfig : ScriptableObject
    {
        [Header("Velocidades (m/s)")]
        [Min(0f)] public float crouchSpeed = 1.5f;
        [Min(0f)] public float walkSpeed = 2.5f;
        [Min(0f)] public float jogSpeed = 4.0f;
        [Min(0f)] public float sprintSpeed = 5.5f;

        [Header("Aceleración (m/s²)")]
        [Min(0.01f)] public float acceleration = 20f;
        [Min(0.01f)] public float deceleration = 25f;

        [Header("Giro")]
        [Tooltip("Grados por segundo al girar hacia la dirección de avance.")]
        [Min(1f)] public float turnSpeed = 720f;

        [Header("Joystick")]
        [Tooltip("Inclinación mínima para moverse.")]
        [Range(0f, 1f)] public float stickDeadzone = 0.15f;
        [Tooltip("Inclinación a partir de la cual se pasa de Walk a Jog.")]
        [Range(0f, 1f)] public float jogThreshold = 0.7f;

        [Header("Altura")]
        [Min(0.5f)] public float standHeight = 1.8f;
        [Min(0.5f)] public float crouchHeight = 1.1f;
        [Tooltip("Metros de altura por segundo al agacharse o levantarse.")]
        [Min(0.1f)] public float crouchTransitionSpeed = 6f;

        [Header("Gravedad")]
        public float gravity = -20f;
        [Tooltip("Velocidad vertical que mantiene al personaje pegado al suelo en pendientes.")]
        public float groundedStickVelocity = -2f;
    }
}
