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
        [Tooltip("Golpes (y derribos) que aguanta antes de romperse.")]
        [Min(1)] public int clubDurability = 6;
        [Tooltip("Segundos que el Lurker queda aturdido tras un golpe (la ventana para escapar). Nunca muere.")]
        [Min(0f)] public float clubStagger = 0.8f;
        [Tooltip("Ruido del impacto (el swing al aire es silencioso).")]
        [Min(0f)] public float clubHitNoise = 5f;
        [Min(0f)] public float clubBreakNoise = 12f;
        [Tooltip("Pausa breve del juego al conectar (hit-stop), en segundos reales.")]
        [Min(0f)] public float hitStop = 0.06f;
        [Tooltip("Escala de tiempo durante el hit-stop.")]
        [Range(0f, 1f)] public float hitStopScale = 0.04f;
        [Tooltip("Embestida hacia adelante al soltar el golpe (m/s): el cuerpo acompaña el swing.")]
        [Min(0f)] public float clubLunge = 3.2f;
        [Tooltip("Retroceso del Lurker al recibir el golpe (m/s).")]
        [Min(0f)] public float clubKnockback = 4f;
        [Tooltip("Fuerza de la reacción física del Lurker (1 = golpe normal); por sorpresa se multiplica por sneakMultiplier.")]
        [Min(0f)] public float clubHitReaction = 1.3f;
        [Tooltip("Golpe de cámara al conectar.")]
        [Min(0f)] public float clubCameraKick = 0.7f;
        [Tooltip("Segundos para sacar el garrote de la mochila (o guardarlo).")]
        [Min(0.05f)] public float clubDrawTime = 0.35f;
        [Tooltip("Un Lurker cazando a esta distancia (m) hace que el papá desenfunde solo.")]
        [Min(0f)] public float clubDrawThreatRadius = 6f;
        [Tooltip("Segundos sin amenaza ni golpes antes de volver a enfundar.")]
        [Min(0f)] public float clubSheathDelay = 3f;
        [Tooltip("Velocidad del jugador mientras golpea (multiplicador).")]
        [Range(0f, 1f)] public float swingMoveFactor = 0.35f;

        [Header("ACCIÓN contextual (POLISH_PASS 3.3)")]
        [Tooltip("Lurker a esta distancia (m): el botón ACCIÓN golpea (con garrote) o empuja (sin él).")]
        [Min(0f)] public float strikeContextRange = 2.2f;
        [Tooltip("Derribo por la espalda: distancia máxima (m).")]
        [Min(0f)] public float takedownRange = 1.8f;
        [Tooltip("Por la espalda = el papá está detrás del Lurker (producto escalar de su frente con la dirección al papá menor que esto).")]
        [Range(-1f, 1f)] public float takedownBehindDot = -0.25f;
        [Tooltip("Segundos que el Lurker queda en el suelo tras un derribo (luego se levanta buscando).")]
        [Min(0f)] public float takedownStun = 20f;
        [Tooltip("Empujón sin garrote: aturdimiento corto, retroceso del Lurker y del papá (m/s).")]
        [Min(0f)] public float shoveStun = 0.6f;
        [Min(0f)] public float shoveKnockback = 3.5f;
        [Min(0f)] public float shoveRecoil = 2.5f;

        [Header("Botón OBJETO (POLISH_PASS 3.1)")]
        [Tooltip("Alcance máximo del lanzamiento (m).")]
        [Min(1f)] public float objectMaxRange = 12f;
        [Tooltip("Sin objetivo: distancia del lanzamiento hacia donde mira el papá.")]
        [Min(1f)] public float objectDefaultRange = 8f;
        [Tooltip("Ángulo (°) delante del papá en el que se busca un Lurker como objetivo.")]
        [Range(10f, 180f)] public float objectAheadAngle = 140f;
        [Tooltip("La piedra cae esta distancia (m) más allá del Lurker: lo aleja del papá.")]
        [Min(0f)] public float stoneOvershoot = 3f;
        [Tooltip("Arrastre (pt) que corresponde al alcance máximo al apuntar en táctil.")]
        [Min(10f)] public float aimDragForMaxRange = 220f;
        [Tooltip("Alcance mínimo al apuntar (m).")]
        [Min(0f)] public float aimMinRange = 2f;

        [Header("Bengala de mano (reemplaza a la pistola, 2026-10-20)")]
        [Tooltip("La pistola de bengalas queda desactivada: Fire usa la bengala de mano.")]
        public bool flareGun = false;
        [Tooltip("Segundos de mecha antes de encenderse (tiempo para llegar a un escondite).")]
        [Min(0f)] public float handFlareFuse = 3f;
        [Min(0f)] public float handFlareBurn = 16f;
        [Tooltip("Últimos segundos en los que la luz se apaga.")]
        [Min(0.1f)] public float handFlareFadeTime = 1.5f;
        [Min(0.2f)] public float handFlareNoiseInterval = 1.5f;
        [Tooltip("Los Lurker dentro de este radio van a la bengala y se quedan mirándola.")]
        [Min(0f)] public float handFlareLureRadius = 16f;
        [Tooltip("Desde que se pulsa hasta que la bengala toca el suelo (animación de dejarla).")]
        [Min(0f)] public float handFlarePlaceDelay = 0.35f;
        [Min(0f)] public float handFlarePlaceDistance = 0.7f;

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
