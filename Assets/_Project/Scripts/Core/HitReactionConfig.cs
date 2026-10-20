using UnityEngine;

namespace ProjectRidge.Core
{
    /// <summary>Reacción física a los golpes (resortes sobre columna, cabeza y brazos encima de la animación).</summary>
    [CreateAssetMenu(menuName = "Project Ridge/Hit Reaction Config", fileName = "HitReactionConfig")]
    public class HitReactionConfig : ScriptableObject
    {
        [Tooltip("Rigidez del resorte (más alto = vuelve antes).")]
        [Min(1f)] public float stiffness = 170f;
        [Tooltip("Amortiguación (bajo = rebota más: orgánico; alto = seco).")]
        [Min(0f)] public float damping = 11f;
        [Tooltip("Grados por segundo de impulso por unidad de fuerza del golpe.")]
        [Min(0f)] public float impulseScale = 520f;
        [Tooltip("Giro aleatorio del torso (fracción del impulso).")]
        [Range(0f, 1f)] public float twist = 0.35f;
        [Tooltip("Ángulo máximo acumulado (grados).")]
        [Min(1f)] public float maxAngle = 55f;

        [Header("Reparto por hueso")]
        [Range(0f, 1f)] public float spine = 0.35f;
        [Range(0f, 1f)] public float chest = 0.45f;
        [Range(0f, 1f)] public float neck = 0.3f;
        [Range(0f, 1f)] public float head = 0.55f;
        [Tooltip("Los brazos se quedan atrás (inercia) en sentido contrario.")]
        [Range(0f, 1f)] public float arms = 0.6f;
    }
}
