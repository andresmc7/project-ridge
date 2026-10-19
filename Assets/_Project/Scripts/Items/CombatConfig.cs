using UnityEngine;

namespace ProjectRidge.Items
{
    /// <summary>Garrote de madera y pistola de bengalas (decisión fija del PoC).</summary>
    [CreateAssetMenu(menuName = "Project Ridge/Combat Config", fileName = "CombatConfig")]
    public class CombatConfig : ScriptableObject
    {
        [Header("Garrote")]
        [Min(0f)] public float clubDamage = 34f;
        [Tooltip("Multiplicador si el enemigo no te está cazando (golpe por sorpresa).")]
        [Min(1f)] public float sneakMultiplier = 2f;
        [Min(0.1f)] public float clubRange = 1.8f;
        [Range(10f, 360f)] public float clubArc = 120f;
        [Min(0f)] public float clubWindup = 0.16f;
        [Min(0f)] public float clubActive = 0.12f;
        [Min(0f)] public float clubRecovery = 0.42f;
        [Tooltip("Golpes que aguanta antes de romperse.")]
        [Min(1)] public int clubDurability = 6;
        [Tooltip("Segundos que el enemigo queda tambaleándose tras un golpe.")]
        [Min(0f)] public float clubStagger = 0.8f;
        [Tooltip("Ruido del impacto (el swing al aire es silencioso).")]
        [Min(0f)] public float clubHitNoise = 5f;
        [Min(0f)] public float clubBreakNoise = 12f;
        [Tooltip("Pausa breve del juego al conectar (hit-stop), en segundos reales.")]
        [Min(0f)] public float hitStop = 0.06f;
        [Tooltip("Con el garrote roto, Attack fabrica uno nuevo con chatarra.")]
        [Min(1)] public int craftScrapCost = 2;
        [Min(0f)] public float craftTime = 1.2f;
        [Min(0f)] public float craftNoise = 3f;
        [Tooltip("Velocidad del jugador mientras golpea (multiplicador).")]
        [Range(0f, 1f)] public float swingMoveFactor = 0.35f;

        [Header("Pistola de bengalas")]
        [Min(0f)] public float shotNoise = 22f;
        [Min(0f)] public float fireCooldown = 0.9f;
        [Tooltip("Velocidad horizontal de la bengala (m/s).")]
        [Min(1f)] public float flareSpeed = 15f;
        [Tooltip("Gravedad aplicada a la bengala en vuelo (m/s²).")]
        public float flareGravity = -10f;
        [Tooltip("Distancia del disparo si no hay objetivo.")]
        [Min(1f)] public float freeShotDistance = 14f;
        [Min(0f)] public float autoAimRange = 18f;
        [Range(10f, 360f)] public float autoAimAngle = 80f;
        [Tooltip("A esta distancia o menos el disparo aturde directamente.")]
        [Min(0f)] public float pointBlankRange = 3.5f;
        [Min(0f)] public float pointBlankStun = 4f;
        [Min(0f)] public float pointBlankDamage = 40f;
        [Tooltip("Impacto directo de la bengala en vuelo.")]
        [Min(0f)] public float directHitStun = 2.5f;
        [Min(0f)] public float directHitDamage = 25f;

        [Header("Bengala ardiendo")]
        [Min(0f)] public float burnTime = 14f;
        [Tooltip("Cada cuánto la bengala vuelve a hacer ruido (atrae enemigos).")]
        [Min(0.2f)] public float burnNoiseInterval = 2f;
        [Min(0f)] public float burnNoiseRadius = 20f;
        [Min(0f)] public float lightIntensity = 7f;
        [Min(0f)] public float lightRange = 11f;
        public Color lightColor = new Color(1f, 0.28f, 0.16f);
    }
}
