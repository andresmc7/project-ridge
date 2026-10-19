using UnityEngine;

namespace ProjectRidge.Items
{
    [CreateAssetMenu(menuName = "Project Ridge/Interaction Config", fileName = "InteractionConfig")]
    public class InteractionConfig : ScriptableObject
    {
        [Tooltip("Distancia máxima para interactuar (m).")]
        [Min(0.5f)] public float radius = 1.8f;
        [Tooltip("Ruido al recoger un objeto.")]
        [Min(0f)] public float pickupNoiseRadius = 2.5f;
        [Tooltip("Ruido al registrar un contenedor.")]
        [Min(0f)] public float searchNoiseRadius = 5f;
        [Tooltip("Vida que devuelve un botiquín.")]
        [Min(1f)] public float medkitHeal = 50f;
    }
}
