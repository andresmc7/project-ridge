using UnityEngine;
using UnityEngine.UI;

namespace ProjectRidge.UI
{
    /// <summary>
    /// Tamaño físico constante del HUD en iPhone y iPad. Con "Scale With Screen Size" un iPad (4:3, 2 px por punto)
    /// agrandaba todo ~70% (el botón ACCIÓN medía 191–225 pt). En móvil el canvas se escala a <see cref="UnitsPerPoint"/>
    /// unidades por punto de UIKit; en PC se deja el escalado por pantalla.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class HudScaler : MonoBehaviour
    {
        /// <summary>Unidades de diseño por punto (HudBuilder.Pt): 1 pt = 2.49 unidades, medido en un iPhone de 6.1".</summary>
        public const float UnitsPerPoint = 2.49f;

        /// <summary>Píxeles por punto simulados para las pruebas en el editor (null = el equipo real).</summary>
        public static float? SimulatedPixelsPerPoint;

        private CanvasScaler scaler;
        private CanvasScaler.ScaleMode defaultMode;

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            defaultMode = scaler.uiScaleMode;
        }

        private void Update() => Apply();

        public void Apply()
        {
            if (scaler == null) Awake();
            float ppp = PixelsPerPoint();
            if (ppp <= 0f) { scaler.uiScaleMode = defaultMode; return; }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = ppp / UnitsPerPoint;
        }

        /// <summary>Píxeles por punto del equipo: 3 en iPhone (≥ 350 ppi), 2 en iPad; 0 fuera de móvil.</summary>
        public static float PixelsPerPoint()
        {
            if (SimulatedPixelsPerPoint.HasValue) return SimulatedPixelsPerPoint.Value;
            if (!Application.isMobilePlatform) return 0f;
            float dpi = Screen.dpi;
            if (dpi <= 0f) return Mathf.Max(Screen.width, Screen.height) / (float)Mathf.Min(Screen.width, Screen.height) > 1.8f ? 3f : 2f;
            return dpi >= 350f ? 3f : 2f;
        }
    }
}
