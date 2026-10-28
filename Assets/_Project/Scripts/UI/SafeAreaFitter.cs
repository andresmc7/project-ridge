using UnityEngine;

namespace ProjectRidge.UI
{
    /// <summary>Ajusta el RectTransform al Screen.safeArea (notch, Dynamic Island, barra de inicio).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        /// <summary>
        /// Área segura simulada (normalizada, 0–1) para las pruebas de diseño en el editor, donde Screen.safeArea es
        /// siempre la pantalla entera. Null = la real.
        /// </summary>
        public static Rect? Simulated;

        private RectTransform rectTransform;
        private Rect appliedArea;
        private Vector2Int appliedScreen;

        private void Awake() => rectTransform = GetComponent<RectTransform>();

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Simulated.HasValue) return;
            if (Screen.safeArea != appliedArea || Screen.width != appliedScreen.x || Screen.height != appliedScreen.y)
                Apply();
        }

        private void Apply()
        {
            if (Simulated.HasValue) { Fit(GetComponent<RectTransform>(), Simulated.Value); return; }
            appliedArea = Screen.safeArea;
            appliedScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Fit(rectTransform != null ? rectTransform : GetComponent<RectTransform>(),
                Rect.MinMaxRect(appliedArea.xMin / Screen.width, appliedArea.yMin / Screen.height, appliedArea.xMax / Screen.width, appliedArea.yMax / Screen.height));
        }

        /// <summary>Lleva el rect a un área normalizada de la pantalla.</summary>
        public static void Fit(RectTransform rt, Rect normalized)
        {
            rt.anchorMin = normalized.min;
            rt.anchorMax = normalized.max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
