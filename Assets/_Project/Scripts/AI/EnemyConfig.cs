using UnityEngine;

namespace ProjectRidge.AI
{
    [CreateAssetMenu(menuName = "Project Ridge/Enemy Config", fileName = "EnemyConfig")]
    public class EnemyConfig : ScriptableObject
    {
        [Header("Velocidades (m/s)")]
        [Min(0f)] public float patrolSpeed = 1.2f;
        [Min(0f)] public float investigateSpeed = 2.2f;
        [Min(0f)] public float searchSpeed = 1.8f;
        [Min(0f)] public float chaseSpeed = 4.8f;

        [Header("Vista")]
        [Min(0f)] public float viewDistance = 14f;
        [Range(10f, 360f)] public float viewAngle = 110f;
        [Tooltip("A esta distancia te detecta aunque estés fuera de su campo de visión (te siente).")]
        [Min(0f)] public float proximityRadius = 1.8f;
        [Tooltip("Dentro de proximityRadius solo te siente si tu visibilidad supera esto (agachado y despacio por la espalda no).")]
        [Range(0f, 2f)] public float proximityMinVisibility = 0.55f;
        [Tooltip("Segundos entre chequeos de percepción (no hace falta cada frame).")]
        [Min(0.01f)] public float perceptionInterval = 0.1f;

        [Header("Sospecha (0–1)")]
        [Tooltip("Sospecha por segundo viéndote de cerca con visibilidad 1.")]
        [Min(0f)] public float suspicionGainNear = 2.5f;
        [Tooltip("Sospecha por segundo viéndote al límite de su vista con visibilidad 1.")]
        [Min(0f)] public float suspicionGainFar = 0.35f;
        [Min(0f)] public float suspicionDecay = 0.2f;
        [Range(0f, 1f)] public float investigateThreshold = 0.35f;
        [Tooltip("Con esta sospecha y viéndote, pasa a Chase.")]
        [Range(0f, 1f)] public float chaseThreshold = 1f;

        [Header("Oído")]
        [Tooltip("Multiplica el radio de los ruidos.")]
        [Min(0f)] public float hearingSensitivity = 1f;
        [Tooltip("Error de posición al oír, como fracción de la distancia (no sabe exactamente dónde fue).")]
        [Range(0f, 1f)] public float hearingPositionError = 0.15f;

        [Header("Patrulla")]
        [Min(0f)] public float patrolPause = 1.5f;

        [Header("Investigar")]
        [Tooltip("Quietud total al oír algo, antes de moverse.")]
        [Min(0f)] public float alertFreeze = 0.9f;
        [Min(0.1f)] public float arriveDistance = 0.6f;
        [Tooltip("Si no llega en este tiempo, empieza a buscar donde esté.")]
        [Min(1f)] public float investigateTimeout = 12f;

        [Header("Buscar")]
        [Min(0f)] public float searchDuration = 10f;
        [Min(0f)] public float searchRadius = 6f;
        [Min(0f)] public float searchLookPause = 1.2f;
        [Tooltip("Grados por segundo al mirar alrededor durante la búsqueda.")]
        [Min(0f)] public float searchTurnSpeed = 120f;

        [Header("Navegación")]
        [Tooltip("Distancia máxima para proyectar un destino sobre el NavMesh.")]
        [Min(0.1f)] public float navSampleDistance = 3f;
        [Tooltip("Segundos entre recálculos de ruta al perseguir o seguir un objetivo.")]
        [Min(0.02f)] public float repathInterval = 0.2f;

        [Header("Persecución")]
        [Tooltip("Sin verte durante este tiempo, pierde el rastro y busca en la última posición conocida.")]
        [Min(0f)] public float loseSightTime = 2.5f;
        [Tooltip("Desplazamiento lateral del zig-zag al perseguir.")]
        [Min(0f)] public float zigzagAmplitude = 1.4f;
        [Min(0f)] public float zigzagFrequency = 1.1f;
        [Tooltip("Por debajo de esta distancia deja de hacer zig-zag y va directo.")]
        [Min(0f)] public float zigzagMinDistance = 4f;

        [Header("Ataque")]
        [Min(0f)] public float attackRange = 1.7f;
        [Min(0f)] public float attackWindup = 0.45f;
        [Min(0f)] public float attackActive = 0.15f;
        [Min(0f)] public float attackRecovery = 1.0f;
        [Min(0f)] public float attackDamage = 35f;
        [Tooltip("Ángulo frontal en el que el golpe conecta.")]
        [Range(10f, 360f)] public float attackArc = 100f;

        [Header("Vida")]
        [Min(1f)] public float maxHealth = 100f;

        [Header("Feedback (ojos)")]
        [ColorUsage(false, true)] public Color eyesCalm = new Color(0.6f, 0.02f, 0.02f);
        [ColorUsage(false, true)] public Color eyesAlert = new Color(2.5f, 0.15f, 0.05f);
        [ColorUsage(false, true)] public Color eyesHunt = new Color(6f, 0.1f, 0.05f);
    }
}
