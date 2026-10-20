using UnityEngine;
using UnityEngine.AI;

namespace ProjectRidge.Player
{
    /// <summary>
    /// Red de seguridad: recuerda el último punto firme sobre el NavMesh y, si el papá cae más de lo permitido
    /// (por un borde que se escapó al diseño), lo devuelve ahí. Nunca debería hacer falta; si se activa, lo registra.
    /// </summary>
    public class PlayerFallRecovery : MonoBehaviour
    {
        [SerializeField] private PlayerController controller;
        [SerializeField] private CharacterController body;
        [Tooltip("Caída (m) bajo el último punto seguro que cuenta como 'se salió del escenario'.")]
        [Min(0.5f)] [SerializeField] private float maxDrop = 1.6f;
        [Tooltip("Segundos entre muestras del punto seguro.")]
        [Min(0.05f)] [SerializeField] private float sampleInterval = 0.4f;

        /// <summary>Veces que tuvo que rescatar al papá (QA: debería ser 0).</summary>
        public static int Recoveries { get; private set; }

        private Vector3 lastSafe;
        private bool hasSafe;
        private float nextSample;

        /// <summary>Olvida el punto seguro (tras un respawn o un teletransporte de guion).</summary>
        public void Forget() => hasSafe = false;

        private void Update()
        {
            if (!body.enabled || !controller.IsGrounded) return;
            Vector3 p = transform.position;
            if (hasSafe && p.y < lastSafe.y - maxDrop)
            {
                Recoveries++;
                Debug.LogWarning("Caída fuera del escenario en " + p + "; vuelta a " + lastSafe);
                body.enabled = false;
                transform.position = lastSafe;
                body.enabled = true;
                return;
            }
            if (Time.time < nextSample) return;
            nextSample = Time.time + sampleInterval;
            // Solo cuenta como seguro si no está más bajo que el anterior (no "aprender" el fondo de una caída).
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 0.5f, NavMesh.AllAreas) && (!hasSafe || p.y > lastSafe.y - 0.6f || (p - lastSafe).sqrMagnitude > 64f))
            {
                lastSafe = p;
                hasSafe = true;
            }
        }
    }
}
