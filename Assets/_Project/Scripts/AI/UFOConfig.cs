using UnityEngine;

namespace ProjectRidge.AI
{
    [CreateAssetMenu(menuName = "Project Ridge/UFO Config", fileName = "UFOConfig")]
    public class UFOConfig : ScriptableObject
    {
        [Header("Posición del OVNI respecto al punto iluminado")]
        [Tooltip("Altura sobre el punto iluminado (m).")]
        [Min(1f)] public float altitude = 13f;
        [Tooltip("Desplazamiento horizontal: la luz cae inclinada (~37°) y los obstáculos proyectan sombra donde esconderse.")]
        [Min(0f)] public float horizontalOffset = 17f;
        [Tooltip("Dirección (en grados, eje Y) desde la que llega la luz.")]
        public float offsetBearing = 35f;

        [Header("Haz")]
        [Range(5f, 80f)] public float beamAngle = 26f;
        [Min(1f)] public float beamRange = 45f;
        [Min(0f)] public float lightIntensity = 120f;
        public Color lightColor = new Color(0.78f, 0.95f, 1f);

        [Header("Escaneo")]
        [Tooltip("Velocidad del punto iluminado al barrer (m/s).")]
        [Min(0f)] public float scanSpeed = 3f;
        [Min(0f)] public float pauseAtPoint = 1.5f;

        [Header("Seguimiento al detectarte")]
        [Min(0f)] public float trackSpeed = 4.5f;
        [Tooltip("Segundos que sigue buscando donde te vio antes de volver a escanear.")]
        [Min(0f)] public float trackLinger = 4f;

        [Header("Exposición (0–1)")]
        [Min(0f)] public float exposureGain = 0.6f;
        [Tooltip("Multiplicador de la ganancia agachado. No anula la luz directa en campo abierto.")]
        [Range(0f, 1f)] public float crouchExposureFactor = 0.6f;
        [Min(0f)] public float exposureDecay = 0.35f;
        [Min(0.02f)] public float checkInterval = 0.1f;
        [Tooltip("Margen interno del cono para la detección (fracción del ángulo).")]
        [Range(0.5f, 1f)] public float detectionConeFraction = 0.9f;

        [Header("Zumbido (sintetizado; el OVNI no se ve)")]
        [Range(0f, 1f)] public float humVolume = 0.55f;
        [Min(20f)] public float humBaseFrequency = 52f;
        [Tooltip("Batido lento del volumen (Hz).")]
        [Min(0.05f)] public float humBeatFrequency = 0.6f;
        [Range(0f, 2f)] public float humNoise = 0.6f;
        [Min(1f)] public float humLoopSeconds = 5f;
        [Range(0f, 1f)] public float humSpatialBlend = 0.8f;
        [Min(0f)] public float humMinDistance = 8f;
        [Min(1f)] public float humMaxDistance = 70f;
        [Min(1f)] public float humTrackingVolumeBoost = 1.5f;
        [Min(0.5f)] public float humTrackingPitch = 1.12f;

        [Header("Escalada")]
        [Tooltip("Radio en el que los Lurker reciben la alerta del OVNI.")]
        [Min(0f)] public float alertRadius = 35f;
        [Min(0f)] public float alertCooldown = 8f;
    }
}
