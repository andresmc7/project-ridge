using UnityEngine;

namespace ProjectRidge.CameraRig
{
    [CreateAssetMenu(menuName = "Project Ridge/Iso Camera Config", fileName = "IsoCameraConfig")]
    public class IsoCameraConfig : ScriptableObject
    {
        [Tooltip("Inclinación hacia abajo, en grados.")]
        [Range(20f, 80f)] public float pitch = 58f;
        [Tooltip("Orientación fija alrededor del eje vertical, en grados.")]
        [Range(-180f, 180f)] public float yaw = 45f;
        [Tooltip("Distancia de la cámara al punto de mira, en metros.")]
        [Min(1f)] public float distance = 18f;
        [Tooltip("Altura del punto de mira sobre los pies del jugador.")]
        [Min(0f)] public float targetHeight = 1f;
        [Range(10f, 90f)] public float fieldOfView = 45f;
        [Tooltip("Metros hacia el fondo (norte) que se adelanta el encuadre: el papá queda en el tercio bajo y se ve el bosque de arriba.")]
        public float framingOffset = 0f;
        [Tooltip("Damping de posición del Follow. 0 = rígido.")]
        [Range(0f, 5f)] public float positionDamping = 0.3f;

        [Header("Oclusión (árboles/rocas que tapan al jugador)")]
        [Range(0f, 1f)] public float occludedAlpha = 0.22f;
        [Tooltip("Alfa por segundo al desvanecer/reaparecer.")]
        [Min(0.1f)] public float occlusionFadeSpeed = 4f;
        [Tooltip("Margen alrededor de la línea cámara–jugador (m).")]
        [Min(0f)] public float occlusionPadding = 0.4f;
        [Min(0.02f)] public float occlusionCheckInterval = 0.1f;
    }
}
