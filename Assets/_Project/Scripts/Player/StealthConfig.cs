using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>Qué tan visible es el papá para la IA. 1 = de pie, quieto a medias, sin cobertura.</summary>
    [CreateAssetMenu(menuName = "Project Ridge/Stealth Config", fileName = "StealthConfig")]
    public class StealthConfig : ScriptableObject
    {
        [Range(0f, 1f)] public float crouchVisibility = 0.55f;
        [Range(0f, 1f)] public float tallGrassVisibility = 0.45f;
        [Tooltip("Piso de visibilidad fuera de un escondite.")]
        [Range(0f, 1f)] public float minVisibility = 0.2f;
        [Tooltip("Agachado dentro del pasto alto = escondido: ni los Lurker ni los reflectores te ven. Es la regla central del loop (esconderse, observar, avanzar).")]
        public bool crouchInGrassHides = true;

        [Header("Movimiento")]
        [Tooltip("Multiplicador quieto.")]
        [Min(0f)] public float stillVisibility = 0.75f;
        [Tooltip("Multiplicador a velocidad de sprint (se interpola según la velocidad real).")]
        [Min(0f)] public float sprintVisibility = 1.3f;

        [Header("Linterna")]
        [Tooltip("Llevar la linterna encendida te hace más visible.")]
        [Min(1f)] public float flashlightVisibility = 1.6f;

        [Header("Agachado automático (POLISH_PASS 3.2)")]
        [Tooltip("Densidad de escondite (0–1) a partir de la cual el papá se agacha y queda oculto.")]
        [Range(0f, 1f)] public float hideDensity = 0.5f;
        [Tooltip("Un Lurker despierto a esta distancia (m) cuenta como amenaza cercana.")]
        [Min(0f)] public float threatRadius = 18f;
        [Tooltip("Un haz del OVNI a esta distancia (m) cuenta como amenaza cercana.")]
        [Min(0f)] public float beamThreatRadius = 12f;
        [Tooltip("Distancia (m) a una cobertura baja para agacharse tras ella.")]
        [Min(0f)] public float coverRadius = 1f;
        [Tooltip("Altura (m) entre la que algo cuenta como cobertura baja (roca, tronco, muro bajo).")]
        [Min(0f)] public float coverMinHeight = 0.45f;
        [Min(0f)] public float coverMaxHeight = 1.4f;
        [Tooltip("Capas de la cobertura baja.")]
        public LayerMask coverMask = 1;
        [Tooltip("Segundos seguidos fuera de las condiciones antes de levantarse (histéresis: no parpadea).")]
        [Min(0f)] public float standUpDelay = 0.4f;
        [Tooltip("Velocidad (°/s) con la que se gira de espaldas a la cobertura estando quieto.")]
        [Min(0f)] public float coverTurnSpeed = 220f;
    }
}
