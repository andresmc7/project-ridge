using System.Text;
using ProjectRidge.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectRidge.UI
{
    /// <summary>Overlay de depuración: modo, velocidad, pasto alto y última acción recibida.</summary>
    public class DebugOverlay : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private PlayerController player;
        [SerializeField] private TallGrassDetector tallGrass;
        [SerializeField] private PlayerInputHandler input;

        private readonly StringBuilder builder = new StringBuilder(128);
        private MovementMode shownMode = (MovementMode)(-1);
        private int shownSpeedTenths = -1;
        private bool shownGrass;
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
            if (!dirty && player.Mode == shownMode && speedTenths == shownSpeedTenths && grass == shownGrass) return;

            shownMode = player.Mode;
            shownSpeedTenths = speedTenths;
            shownGrass = grass;
            dirty = false;

            builder.Clear();
            builder.Append("Modo: ").Append(shownMode.ToString()).Append('\n');
            builder.Append("Velocidad: ").Append(speedTenths / 10).Append('.').Append(speedTenths % 10).Append(" m/s\n");
            builder.Append("Pasto alto: ").Append(grass ? "sí" : "no").Append('\n');
            builder.Append("Acción: ").Append(lastAction);
            label.text = builder.ToString();
        }
    }
}
