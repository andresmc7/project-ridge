using ProjectRidge.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectRidge.UI
{
    /// <summary>
    /// Curarse en táctil (POLISH_PASS 3.1): mantener el indicador de vida holdTime segundos usa un botiquín. Un toque
    /// suelto no hace nada (así no se gasta sin querer). El anillo se llena mientras se mantiene.
    /// </summary>
    public class HoldToHeal : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private PlayerHealing healing;
        [SerializeField] private Image progress;
        [Min(0.05f)] [SerializeField] private float holdTime = 0.6f;

        private float since = -1f;

        public void OnPointerDown(PointerEventData e) => since = Time.unscaledTime;
        public void OnPointerUp(PointerEventData e) => since = -1f;
        public void OnPointerExit(PointerEventData e) => since = -1f;

        private void Update()
        {
            float t = since < 0f ? 0f : (Time.unscaledTime - since) / holdTime;
            if (progress != null) progress.fillAmount = Mathf.Clamp01(t);
            if (t < 1f) return;
            since = -1f;
            healing.TryHeal();
        }
    }
}
