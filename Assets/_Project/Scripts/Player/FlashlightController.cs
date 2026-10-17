using UnityEngine;

namespace ProjectRidge.Player
{
    /// <summary>Linterna del papá: luz spot en tiempo real (una de las 4 dinámicas permitidas, con sombras).</summary>
    public class FlashlightController : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private Light spot;
        [SerializeField] private bool startOn;

        public bool IsOn => spot.enabled;

        private void Awake() => spot.enabled = startOn;
        private void OnEnable() => input.FlashlightPressed += Toggle;
        private void OnDisable() => input.FlashlightPressed -= Toggle;

        public void Toggle() => spot.enabled = !spot.enabled;
    }
}
