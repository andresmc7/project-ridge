using UnityEngine;

namespace ProjectRidge.Player
{
    [CreateAssetMenu(menuName = "Project Ridge/Player Health Config", fileName = "PlayerHealthConfig")]
    public class PlayerHealthConfig : ScriptableObject
    {
        [Min(1f)] public float maxHealth = 100f;
        [Tooltip("Segundos entre morir y reaparecer en el punto de inicio.")]
        [Min(0f)] public float respawnDelay = 2f;
        [Tooltip("Al reaparecer, los enemigos a menos de esta distancia del punto de inicio vuelven a su ruta.")]
        [Min(0f)] public float respawnEnemyClearRadius = 20f;
    }
}
