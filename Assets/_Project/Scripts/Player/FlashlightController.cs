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
        /// <summary>0–1: cuánto parpadea (la captura del OVNI la hace fallar).</summary>
        public float Flicker { get; set; }

        private float baseIntensity;

        private void Awake()
        {
            spot.enabled = startOn;
            baseIntensity = spot.intensity;
        }

        private void Update()
        {
            if (Flicker <= 0f) { spot.intensity = baseIntensity; return; }
            // Caídas bruscas y cortas, como un contacto que falla.
            float n = Mathf.PerlinNoise(Time.time * 23f, 0.37f);
            spot.intensity = baseIntensity * (n < Flicker * 0.6f ? 0.05f : 1f - Flicker * 0.4f * n);
        }
        private void OnEnable() => input.FlashlightPressed += Toggle;
        private void OnDisable() => input.FlashlightPressed -= Toggle;

        public void Toggle() => spot.enabled = !spot.enabled;
    }
}
