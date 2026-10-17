using UnityEngine;

namespace ProjectRidge.CameraRig
{
    [CreateAssetMenu(menuName = "Project Ridge/Iso Camera Config", fileName = "IsoCameraConfig")]
    public class IsoCameraConfig : ScriptableObject
    {
        [Tooltip("Inclinación hacia abajo, en grados.")]
        [Range(20f, 80f)] public float pitch = 50f;
        [Tooltip("Orientación fija alrededor del eje vertical, en grados.")]
        [Range(-180f, 180f)] public float yaw = 45f;
        [Tooltip("Distancia de la cámara al punto de mira, en metros.")]
        [Min(1f)] public float distance = 14f;
        [Tooltip("Altura del punto de mira sobre los pies del jugador.")]
        [Min(0f)] public float targetHeight = 1f;
        [Range(10f, 90f)] public float fieldOfView = 40f;
        [Tooltip("Damping de posición del Follow. 0 = rígido.")]
        [Range(0f, 5f)] public float positionDamping = 0.3f;
    }
}
