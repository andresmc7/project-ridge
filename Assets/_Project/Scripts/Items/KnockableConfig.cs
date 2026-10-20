using UnityEngine;

namespace ProjectRidge.Items
{
    /// <summary>Objetos sueltos con presencia física: ramas, latas, cubetas, cajitas que salen volando al chocarlos.</summary>
    [CreateAssetMenu(menuName = "Project Ridge/Knockable Config", fileName = "KnockableConfig")]
    public class KnockableConfig : ScriptableObject
    {
        [Tooltip("El papá solo los patea por encima de esta velocidad (m/s): agachado o caminando pasa sin tocarlos.")]
        [Min(0f)] public float playerKickSpeed = 2.2f;
        [Tooltip("Velocidad horizontal que reciben (m/s) por cada m/s de quien los choca.")]
        [Min(0f)] public float impulsePerSpeed = 1.2f;
        [Min(0f)] public float minImpulse = 2.2f;
        [Min(0f)] public float maxImpulse = 7f;
        [Tooltip("Velocidad hacia arriba al salir volando (m/s).")]
        [Min(0f)] public float upward = 2.4f;
        [Tooltip("Giro aleatorio (rad/s).")]
        [Min(0f)] public float spin = 9f;
        [Tooltip("Ruido al patearlos el papá (atrae Lurkers): moverse rápido entre escombros delata.")]
        [Min(0f)] public float playerNoiseRadius = 7f;
        [Tooltip("Segundos antes de poder volver a reaccionar (evita rebotes dobles).")]
        [Min(0f)] public float cooldown = 0.4f;
    }
}
