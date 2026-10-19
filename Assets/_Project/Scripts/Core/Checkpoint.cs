using UnityEngine;

namespace ProjectRidge.Core
{
    /// <summary>Volumen trigger: al entrar, guarda este punto como reaparición y opcionalmente cambia el objetivo.</summary>
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        [Tooltip("Dónde reaparece el jugador (si está vacío, este transform).")]
        [SerializeField] private Transform spawn;
        [SerializeField] private string objective = "";

        private void OnTriggerEnter(Collider other)
        {
            if (GameFlow.Instance == null || !other.TryGetComponent(out ProjectRidge.Player.PlayerHealth _)) return;
            GameFlow.Instance.ReachCheckpoint(spawn != null ? spawn : transform, objective);
        }
    }
}
