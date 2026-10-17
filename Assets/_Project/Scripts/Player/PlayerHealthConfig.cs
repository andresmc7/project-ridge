using UnityEngine;

namespace ProjectRidge.Player
{
    [CreateAssetMenu(menuName = "Project Ridge/Player Health Config", fileName = "PlayerHealthConfig")]
    public class PlayerHealthConfig : ScriptableObject
    {
        [Min(1f)] public float maxHealth = 100f;
    }
}
