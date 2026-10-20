using UnityEngine;

namespace ProjectRidge.Player
{
    [CreateAssetMenu(menuName = "Project Ridge/Player Movement Config", fileName = "PlayerMovementConfig")]
    public class PlayerMovementConfig : ScriptableObject
    {
        [Header("Velocidades (m/s)")]
        [Min(0f)] public float crouchSpeed = 1.5f;
        [Tooltip("Caminar sigiloso de pie (joystick apenas inclinado). Usa el clip de caminar a su escala mínima.")]
        [Min(0f)] public float sneakSpeed = 0.8f;
        [Min(0f)] public float walkSpeed = 2.5f;
        [Min(0f)] public float jogSpeed = 4.0f;
        [Min(0f)] public float sprintSpeed = 5.5f;

        [Header("Aceleración (m/s²)")]
        [Min(0.01f)] public float acceleration = 40f;
        [Min(0.01f)] public float deceleration = 55f;

        [Header("Giro")]
        [Tooltip("Grados por segundo al girar hacia la dirección de avance (540: media vuelta en 0.33 s, con peso).")]
        [Min(1f)] public float turnSpeed = 540f;
        [Tooltip("Cuánto frena el avance mientras el cuerpo aún no encara el input (1 = coseno; más alto = pivota casi en el sitio).")]
        [Min(0.1f)] public float alignmentSharpness = 2f;

        [Header("Joystick")]
        [Tooltip("Inclinación mínima para moverse.")]
        [Range(0f, 1f)] public float stickDeadzone = 0.15f;
        [Tooltip("Por debajo: caminar sigiloso (lento y casi mudo).")]
        [Range(0f, 1f)] public float sneakThreshold = 0.35f;
        [Tooltip("Desde sneakThreshold hasta aquí la velocidad pasa de caminar a trotar de forma continua.")]
        [Range(0f, 1f)] public float jogThreshold = 0.85f;
        [Tooltip("Inclinación que cuenta como \"al borde\" para fijar el sprint (ya con la zona muerta del stick aplicada: 0.925 físico = 1).")]
        [Range(0f, 1f)] public float sprintEdge = 0.98f;
        [Tooltip("Segundos al borde para fijar el sprint (se suelta al soltar o frenar).")]
        [Min(0f)] public float sprintHoldTime = 0.25f;

        [Header("Altura")]
        [Min(0.5f)] public float standHeight = 1.8f;
        [Min(0.5f)] public float crouchHeight = 1.1f;
        [Tooltip("Metros de altura por segundo al agacharse o levantarse.")]
        [Min(0.1f)] public float crouchTransitionSpeed = 6f;

        [Header("Empujones")]
        [Tooltip("Frenado de un empujón externo (m/s²): golpe recibido, embestida al golpear.")]
        [Min(0.1f)] public float knockbackDrag = 14f;

        [Header("Gravedad")]
        public float gravity = -20f;
        [Tooltip("Velocidad vertical que mantiene al personaje pegado al suelo en pendientes.")]
        public float groundedStickVelocity = -2f;
    }
}
