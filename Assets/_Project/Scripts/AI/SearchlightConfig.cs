using UnityEngine;

namespace ProjectRidge.AI
{
    /// <summary>
    /// Reflectores rojos de la nave: círculos de luz que recorren rutas fijas. Son el obstáculo principal del loop
    /// (esconderse en el pasto, observar el patrón, avanzar al siguiente escondite).
    /// </summary>
    [CreateAssetMenu(menuName = "Project Ridge/Searchlight Config", fileName = "SearchlightConfig")]
    public class SearchlightConfig : ScriptableObject
    {
        [Header("Círculo de luz")]
        [Tooltip("Radio del círculo en el suelo (m).")]
        [Min(0.5f)] public float spotRadius = 2.3f;
        [Tooltip("Fracción del radio que detecta (el borde brillante es aviso, no castigo).")]
        [Range(0.5f, 1f)] public float detectionFraction = 0.85f;
        [Tooltip("Diferencia de altura máxima entre el papá y el círculo para que cuente (puentes, cornisas).")]
        [Min(0.5f)] public float verticalTolerance = 2.5f;

        [Header("Exposición")]
        [Tooltip("Segundos bajo la luz, sin esconderse, hasta que la nave te lleva.")]
        [Min(0.1f)] public float exposeTime = 0.65f;
        [Tooltip("Cuánto baja la exposición por segundo fuera de la luz.")]
        [Min(0f)] public float exposureDecay = 1.6f;
        [Tooltip("Al empezar a llenarse, alerta a los Lurker a esta distancia.")]
        [Min(0f)] public float alertRadius = 12f;

        [Header("Haz visible")]
        [Tooltip("Altura de la nave sobre el círculo (m).")]
        [Min(4f)] public float beamHeight = 22f;
        [Tooltip("La luz llega desde el norte (fondo de la pantalla), nunca cruza la cámara.")]
        [Min(0f)] public float beamBackOffset = 9f;
        public Color color = new Color(1f, 0.12f, 0.07f);
        [Tooltip("Color cuando te está llenando la exposición.")]
        public Color alarmColor = new Color(1f, 0.75f, 0.55f);

        [Header("Luz real (una sola, en el reflector más cercano al papá)")]
        [Min(0f)] public float lightIntensity = 90f;
        [Min(1f)] public float lightRange = 40f;
        [Min(1f)] public float proxyMaxDistance = 24f;
    }
}
