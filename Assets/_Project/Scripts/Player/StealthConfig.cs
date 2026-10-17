using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>Qué tan visible es el papá para la IA. 1 = de pie, quieto a medias, sin cobertura.</summary>
    [CreateAssetMenu(menuName = "Project Ridge/Stealth Config", fileName = "StealthConfig")]
    public class StealthConfig : ScriptableObject
    {
        [Range(0f, 1f)] public float crouchVisibility = 0.55f;
        [Range(0f, 1f)] public float tallGrassVisibility = 0.45f;
        [Tooltip("Piso: agacharse en pasto alto nunca vuelve invisible al jugador.")]
        [Range(0f, 1f)] public float minVisibility = 0.2f;

        [Header("Movimiento")]
        [Tooltip("Multiplicador quieto.")]
        [Min(0f)] public float stillVisibility = 0.75f;
        [Tooltip("Multiplicador a velocidad de sprint (se interpola según la velocidad real).")]
        [Min(0f)] public float sprintVisibility = 1.3f;

        [Header("Linterna")]
        [Tooltip("Llevar la linterna encendida te hace más visible.")]
        [Min(1f)] public float flashlightVisibility = 1.6f;
    }
}
