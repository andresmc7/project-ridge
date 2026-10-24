using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Ajustes de la animación del papá (POLISH_PASS 3.7): arranques y frenadas, pivote, idle con vida, inclinación en
    /// curvas e IK de pies. Los giros en el sitio están en PlayerMovementConfig (los hace el controlador).
    /// </summary>
    [CreateAssetMenu(menuName = "Project Ridge/Father Animation Config", fileName = "FatherAnimationConfig")]
    public class FatherAnimationConfig : ScriptableObject
    {
        [Header("Arranques y frenadas")]
        [Tooltip("Por debajo de esta velocidad (m/s) el papá está parado: empujar el stick dispara el clip de arranque.")]
        [Min(0f)] public float restSpeed = 0.15f;
        [Tooltip("Al soltar el stick por encima de esta velocidad (m/s) se reproduce una frenada.")]
        [Min(0f)] public float stopMinSpeed = 0.5f;
        [Tooltip("Desde esta velocidad (m/s) la frenada es la de carrera.")]
        [Min(0f)] public float runStopSpeed = 1.9f;

        [Header("Pivote en carrera")]
        [Tooltip("Ángulo (°) entre el cuerpo y el input que cuenta como media vuelta.")]
        [Range(90f, 180f)] public float pivotAngle = 150f;
        [Min(0f)] public float pivotMinSpeed = 1.9f;
        [Min(0f)] public float pivotCooldown = 0.8f;

        [Header("Giro en el sitio")]
        [Tooltip("Segundos de la parte que gira en los clips de giro (a 1x); la velocidad del estado se ajusta al giro real.")]
        [Min(0.05f)] public float turnClipTurnTime = 0.6f;
        [Min(0.1f)] public float minTurnRate = 0.85f;
        [Min(0.1f)] public float maxTurnRate = 1.6f;

        [Header("Idle con vida")]
        [Tooltip("Segundos quieto antes de mirar alrededor asustado.")]
        [Min(0f)] public float lookAroundAfter = 6f;
        [Tooltip("Segundos entre una mirada y la siguiente si sigue quieto.")]
        [Min(0f)] public float lookAroundRepeat = 9f;
        [Tooltip("Con un Lurker a menos de esta distancia (m), idle tenso (encorvado, respiración rápida).")]
        [Min(0f)] public float tenseRadius = 14f;
        [Tooltip("Peso máximo del idle tenso sobre el normal: a 1 el estilo \"BentForward\" lo dobla casi a la mitad.")]
        [Range(0f, 1f)] public float tenseIdleWeight = 0.5f;
        [Tooltip("Segundos para entrar o salir del idle tenso.")]
        [Min(0.01f)] public float tenseBlendTime = 0.8f;

        [Header("Inclinación en curvas")]
        [Tooltip("Grados de inclinación lateral por cada grado/segundo de giro.")]
        [Min(0f)] public float curveLeanPerDegPerSec = 0.02f;
        [Tooltip("Inclinación máxima (°).")]
        [Min(0f)] public float maxCurveLean = 7f;
        [Min(0.01f)] public float curveLeanSmoothing = 8f;
        [Tooltip("Por debajo de esta velocidad (m/s) no hay inclinación de curva (giros en el sitio).")]
        [Min(0f)] public float curveLeanMinSpeed = 0.6f;

        [Header("IK de pies")]
        [Tooltip("Capas del suelo (terreno, puente, rocas).")]
        public LayerMask groundMask = ~((1 << 2) | (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11) | (1 << 12)); // sin Ignore Raycast, TallGrass, Player, Enemy, Canopy, Debris
        [Tooltip("Desde cuánto por encima del pie se lanza el rayo al suelo (m).")]
        [Min(0f)] public float footRayUp = 0.5f;
        [Tooltip("Largo del rayo hacia abajo desde ese punto (m).")]
        [Min(0f)] public float footRayDown = 1.1f;
        [Tooltip("Altura del tobillo sobre el suelo (m), medida en el idle del papá MakeHuman (7.2 cm).")]
        [Min(0f)] public float ankleHeight = 0.072f;
        [Tooltip("Sobre esta altura del pie (m, en la animación) el IK se apaga: el pie está en el aire dando el paso.")]
        [Min(0.01f)] public float footLiftFade = 0.12f;
        [Tooltip("Cuánto puede bajar la cadera (m) para que el pie más bajo llegue al suelo.")]
        [Min(0f)] public float maxHipDrop = 0.35f;
        [Min(0.01f)] public float hipSmoothing = 12f;

        [Header("Cojera con poca vida (3.12)")]
        [Tooltip("Por debajo de esta fracción de vida cojea.")]
        [Range(0f, 1f)] public float limpBelowHealth = 0.35f;
        [Tooltip("Cuánto baja la cadera (m) al apoyar la pierna herida (la izquierda).")]
        [Min(0f)] public float limpDip = 0.06f;
        [Tooltip("Inclinación lateral (°) hacia la pierna herida al apoyarla.")]
        [Min(0f)] public float limpTilt = 5f;
    }
}
