using UnityEngine;

namespace ProjectRidge.Noise
{
    [CreateAssetMenu(menuName = "Project Ridge/Noise Config", fileName = "NoiseConfig")]
    public class NoiseConfig : ScriptableObject
    {
        [Header("Radio de un paso (m)")]
        [Min(0f)] public float crouchStepRadius = 1.2f;
        [Min(0f)] public float walkStepRadius = 3.5f;
        [Min(0f)] public float jogStepRadius = 7f;
        [Min(0f)] public float sprintStepRadius = 12f;

        [Header("Distancia entre pasos (m)")]
        [Min(0.1f)] public float crouchStride = 0.5f;
        [Min(0.1f)] public float walkStride = 0.75f;
        [Min(0.1f)] public float jogStride = 1.0f;
        [Min(0.1f)] public float sprintStride = 1.3f;

        [Header("Atenuación")]
        [Tooltip("Factor del radio cuando hay un obstáculo sólido entre el ruido y quien escucha.")]
        [Range(0f, 1f)] public float occludedFactor = 0.6f;
    }
}
