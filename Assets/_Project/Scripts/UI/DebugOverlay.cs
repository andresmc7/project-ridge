using System.Text;
using ProjectRidge.AI;
using ProjectRidge.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectRidge.UI
{
    /// <summary>Overlay de depuración: jugador (modo, velocidad, pasto, vida, última acción) y Lurker (estado, sospecha).</summary>
    public class DebugOverlay : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private PlayerController player;
        [SerializeField] private TallGrassDetector tallGrass;
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private PlayerHealth health;
        [Tooltip("Opcional: si está vacío no se muestra la sección del enemigo.")]
        [SerializeField] private EnemyController enemy;
        [SerializeField] private EnemyPerception enemyPerception;

        private readonly StringBuilder builder = new StringBuilder(256);
        private MovementMode shownMode = (MovementMode)(-1);
        private int shownSpeedTenths = -1;
        private bool shownGrass;
        private int shownHealth = -1;
        private EnemyState shownEnemyState = (EnemyState)(-1);
        private int shownSuspicion = -1;
        private string lastAction = "-";
        private bool dirty = true;

        private void OnEnable() => input.ActionPerformed += OnAction;
        private void OnDisable() => input.ActionPerformed -= OnAction;

        private void OnAction(InputAction action)
        {
            lastAction = action.name;
            dirty = true;
        }

        private void Update()
        {
            int speedTenths = Mathf.RoundToInt(player.CurrentSpeed * 10f);
            bool grass = tallGrass.IsInTallGrass;
            int hp = health != null ? Mathf.CeilToInt(health.Current) : -1;
            EnemyState enemyState = enemy != null ? enemy.State : shownEnemyState;
            int suspicion = enemyPerception != null ? Mathf.RoundToInt(enemyPerception.Suspicion * 100f) : -1;

            if (!dirty && player.Mode == shownMode && speedTenths == shownSpeedTenths && grass == shownGrass
                && hp == shownHealth && enemyState == shownEnemyState && suspicion == shownSuspicion) return;

            shownMode = player.Mode;
            shownSpeedTenths = speedTenths;
            shownGrass = grass;
            shownHealth = hp;
            shownEnemyState = enemyState;
            shownSuspicion = suspicion;
            dirty = false;

            builder.Clear();
            builder.Append("Modo: ").Append(shownMode.ToString()).Append('\n');
            builder.Append("Velocidad: ").Append(speedTenths / 10).Append('.').Append(speedTenths % 10).Append(" m/s\n");
            builder.Append("Pasto alto: ").Append(grass ? "sí" : "no").Append('\n');
            if (health != null) builder.Append("Vida: ").Append(hp).Append('\n');
            builder.Append("Acción: ").Append(lastAction);
            if (enemy != null)
            {
                builder.Append("\nLurker: ").Append(enemyState.ToString());
                if (enemyPerception != null) builder.Append("  sospecha ").Append(suspicion).Append('%');
            }
            label.text = builder.ToString();
        }
    }
}
