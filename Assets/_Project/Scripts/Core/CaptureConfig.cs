using UnityEngine;

namespace ProjectRidge.Core
{
    /// <summary>Tiempos y sensaciones de la captura del OVNI (POLISH_PASS 3.4).</summary>
    [CreateAssetMenu(menuName = "Project Ridge/Capture Config", fileName = "CaptureConfig")]
    public class CaptureConfig : ScriptableObject
    {
        [Header("Bloqueo")]
        [Tooltip("El haz se estrecha a esta fracción de su radio sobre el papá.")]
        [Range(0.2f, 1f)] public float beamNarrow = 0.7f;
        [Tooltip("Color del haz al atrapar: blanco frío.")]
        public Color lockColor = new Color(0.82f, 0.9f, 1f);

        [Header("Parálisis")]
        [Min(0.1f)] public float paralysisTime = 1.6f;
        [Tooltip("Frecuencia del temblor (Hz), como una corriente eléctrica.")]
        [Min(1f)] public float tremorHzMin = 18f;
        [Min(1f)] public float tremorHzMax = 25f;
        [Tooltip("Amplitud del temblor (°) al empezar y al final de la parálisis; en la elevación, al máximo.")]
        [Min(0f)] public float tremorStart = 1.5f;
        [Min(0f)] public float tremorEnd = 4f;
        [Min(0f)] public float tremorLift = 6f;
        [Tooltip("La cámara se acerca este porcentaje durante la parálisis.")]
        [Range(0f, 0.5f)] public float cameraZoom = 0.12f;

        [Header("Forcejeo (una vez por vida)")]
        [Min(0.1f)] public float struggleTime = 1.4f;
        [Tooltip("Toques (o vaivenes del joystick) para soltarse.")]
        [Min(1)] public int tapsNeeded = 10;
        [Tooltip("Al soltarse: empujón del papá lejos del haz (m/s), retroceso del haz (m) y segundos que queda ciego.")]
        [Min(0f)] public float breakFreeKnock = 4.5f;
        [Min(0f)] public float beamRecoil = 4f;
        [Min(0f)] public float beamBlind = 2.5f;

        [Header("Elevación (si no se suelta)")]
        [Min(0f)] public float liftHeight = 3f;
        [Min(0.1f)] public float liftTime = 1.5f;
        [Tooltip("Destello blanco al final de la elevación (s).")]
        [Min(0.05f)] public float flashTime = 0.35f;

        [Header("Exposición (antes del bloqueo)")]
        [Tooltip("Peso de la viñeta fría mientras se llena la exposición (1 = la de la parálisis).")]
        [Range(0f, 1f)] public float exposureVignette = 0.4f;
    }
}
